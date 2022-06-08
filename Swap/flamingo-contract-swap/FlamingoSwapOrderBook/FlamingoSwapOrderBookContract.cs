using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;
using System.Numerics;
using System.ComponentModel;

namespace FlamingoSwapOrderBook
{
    [DisplayName("FlamingoSwapOrderBook")]
    [ManifestExtra("Author", "NEO")]
    [ManifestExtra("Email", "developer@neo.org")]
    [ManifestExtra("Description", "This is a FlamingoSwapOrderBook")]
    [ContractPermission("*")]
    public partial class FlamingoSwapOrderBookContract : SmartContract
    {
        public struct LimitOrder
        {
            public UInt160 maker;
            public BigInteger price;
            public BigInteger amount;
            public ByteString nextID;
        }

        public struct OrderReceipt
        {
            public UInt160 baseToken;
            public UInt160 quoteToken;
            public ByteString id;
            public ulong time;
            public bool isBuy;
            public UInt160 maker;
            public BigInteger price;
            public BigInteger totalAmount;
            public BigInteger leftAmount;
        }

        public struct OrderBook
        {
            public UInt160 baseToken;
            public UInt160 quoteToken;
            public uint quoteDecimals;

            public ByteString firstBuyID;
            public ByteString firstSellID;
        }

        #region DEX like API
        /// <summary>
        /// Register a new book
        /// </summary>
        /// <param name="baseToken"></param>
        /// <param name="quoteToken"></param>
        /// <param name="quoteDecimals"></param>
        /// <returns></returns>
        public static bool RegisterOrderBook(UInt160 baseToken, UInt160 quoteToken, uint quoteDecimals)
        {
            Assert(baseToken.IsAddress() && quoteToken.IsAddress(), "Invalid Address");
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");

            var pairKey = GetPairKey(baseToken, quoteToken);
            if (BookExists(pairKey)) return false;
            SetOrderBook(pairKey, new OrderBook(){
                baseToken = baseToken,
                quoteToken = quoteToken,
                quoteDecimals = quoteDecimals
            });
            onRegisterBook(baseToken, quoteToken, quoteDecimals);
            return true;
        }

        /// <summary>
        /// Remove a new book and cancel all existing orders
        /// </summary>
        /// <param name="baseToken"></param>
        /// <param name="quoteToken"></param>
        /// <returns></returns>
        public static bool RemoveOrderBook(UInt160 baseToken, UInt160 quoteToken)
        {
            Assert(baseToken.IsAddress() && quoteToken.IsAddress(), "Invalid Address");
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");

            var pairKey = GetPairKey(baseToken, quoteToken);
            if (!BookExists(pairKey)) return false;
            if (GetBaseToken(pairKey) != baseToken) return false;
            if (GetQuoteToken(pairKey) != quoteToken) return false;

            // Cancel orders
            var firstBuyID = GetFirstOrderID(pairKey, true);
            while (firstBuyID is not null)
            {
                // Remove from book
                var order = GetOrder(firstBuyID);
                Assert(RemoveOrder(pairKey, firstBuyID, true), "Remove Order Fail");
                // Remove receipt
                DeleteReceipt(order.maker, firstBuyID);
                onOrderStatusChanged(baseToken, quoteToken, firstBuyID, true, order.maker, order.price, 0);

                // Sendback token
                SafeTransfer(quoteToken, Runtime.ExecutingScriptHash, order.maker, order.amount * order.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey)));

                // Try again
                firstBuyID = GetFirstOrderID(pairKey, true);
            }

            var firstSellID = GetFirstOrderID(pairKey, false);
            while (firstSellID is not null)
            {
                // Remove from book
                var order = GetOrder(firstSellID);
                Assert(RemoveOrder(pairKey, firstSellID, false), "Remove Order Fail");
                // Remove receipt
                DeleteReceipt(order.maker, firstSellID);
                onOrderStatusChanged(baseToken, quoteToken, firstSellID, false, order.maker, order.price, 0);

                // Sendback token
                SafeTransfer(baseToken, Runtime.ExecutingScriptHash, order.maker, order.amount);

                // Try again
                firstSellID = GetFirstOrderID(pairKey, false);
            }

            // Remove book
            DeleteOrderBook(pairKey);
            onRemoveBook(baseToken, quoteToken);
            return true;
        }

        /// <summary>
        /// Add a new order into orderbook but try deal it first
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="maker"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns>Null or a new order id</returns>
        public static ByteString AddLimitOrder(UInt160 tokenA, UInt160 tokenB, UInt160 maker, bool isBuy, BigInteger price, BigInteger amount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0 && amount > 0, "Invalid Parameters");
            Assert(Runtime.CheckWitness(maker), "No Authorization");

            // Deal as market order
            var leftAmount = DealMarketOrderInternal(pairKey, maker, isBuy, price, amount);
            if (leftAmount == 0) return null;

            // Deposit token
            var me = Runtime.ExecutingScriptHash;
            if (isBuy) SafeTransfer(GetQuoteToken(pairKey), maker, me, leftAmount * price / BigInteger.Pow(10, GetQuoteDecimals(pairKey)));
            else SafeTransfer(GetBaseToken(pairKey), maker, me, leftAmount);

            // Do add
            var id = GetUnusedID();
            Assert(InsertOrder(pairKey, id, new LimitOrder(){
                maker = maker,
                price = price,
                amount = leftAmount
            }, isBuy), "Add Order Fail");
            var baseToken = GetBaseToken(pairKey);
            var quoteToken = GetQuoteToken(pairKey);

            // Add receipt
            SetReceipt(maker, id, new OrderReceipt(){
                baseToken = baseToken,
                quoteToken = quoteToken,
                id = id,
                time = Runtime.Time,
                isBuy = isBuy,
                totalAmount = leftAmount
            });
            onOrderStatusChanged(baseToken, quoteToken, id, isBuy, maker, price, leftAmount);
            return id;
        }

        /// <summary>
        /// Cancel a limit order with its id
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool CancelOrder(UInt160 tokenA, UInt160 tokenB, bool isBuy, ByteString id)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            if (!OrderExists(id)) return false;
            var order = GetOrder(id);
            Assert(Runtime.CheckWitness(order.maker), "No Authorization");

            // Do remove
            var baseToken = GetBaseToken(pairKey);
            var quoteToken = GetQuoteToken(pairKey);
            Assert(RemoveOrder(pairKey, id, isBuy), "Remove Order Fail");
            // Remove receipt
            DeleteReceipt(order.maker, id);
            onOrderStatusChanged(baseToken, quoteToken, id, isBuy, order.maker, order.price, 0);

            // Withdraw token
            var me = Runtime.ExecutingScriptHash;
            if (isBuy) SafeTransfer(GetQuoteToken(pairKey), me, order.maker, order.amount * order.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey)));
            else SafeTransfer(GetBaseToken(pairKey), me, order.maker, order.amount);
            return true;
        }

        /// <summary>
        /// Get first N limit orders and their details
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        [Safe]
        public static OrderReceipt[] GetFirstNOrders(UInt160 tokenA, UInt160 tokenB, bool isBuy, uint n)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            var results = new OrderReceipt[n];
            if (GetFirstOrderID(pairKey, isBuy) is null) return results;
            var currentOrderID = GetFirstOrderID(pairKey, isBuy);
            var currentOrder = GetOrder(currentOrderID);
            for (int i = 0; i < n; i++)
            {
                var receipt = GetReceipt(currentOrder.maker, currentOrderID);
                receipt.maker = currentOrder.maker;
                receipt.price = currentOrder.price;
                receipt.leftAmount = currentOrder.amount;
                results[i] = receipt;

                if (currentOrder.nextID is null) break;
                currentOrderID = currentOrder.nextID;
                currentOrder = GetOrder(currentOrder.nextID);
            }

            return results;
        }

        /// <summary>
        /// Get all orders and their details of maker
        /// </summary>
        /// <param name="maker"></param>
        /// <returns></returns>
        [Safe]
        public static OrderReceipt[] GetOrdersOf(UInt160 maker)
        {
            // Get receipts
            var receipts = GetReceiptsOf(maker);
            // Makeup details
            for (int i = 0; i < receipts.Length; i++)
            {
                var order = GetOrder(receipts[i].id);
                receipts[i].maker = order.maker;
                receipts[i].price = order.price;
                receipts[i].leftAmount = order.amount;
            }
            return receipts;
        }

        /// <summary>
        /// Get the total reverse of tradable orders with an expected price
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <returns>Tradable base amount</returns>
        [Safe]
        public static BigInteger GetTotalTradable(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0, "Invalid Price");

            return GetTotalTradableInternal(pairKey, isBuy, price);
        }

        private static BigInteger GetTotalTradableInternal(byte[] pairKey, bool isBuy, BigInteger price)
        {
            BigInteger totalTradable = 0;
            if (GetFirstOrderID(pairKey, !isBuy) is null) return totalTradable;

            var quoteDecimals = GetQuoteDecimals(pairKey);
            var currentID = GetFirstOrderID(pairKey, !isBuy);

            while (currentID is not null)
            {
                var currentOrder = GetOrder(currentID);
                // Check price
                if ((isBuy && currentOrder.price > price) || (!isBuy && currentOrder.price < price)) break;

                totalTradable += currentOrder.amount;

                currentID = currentOrder.nextID;
            }
            return totalTradable;
        }

        /// <summary>
        /// Try to match without real payment
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns>Left amount and total payment</returns>
        [Safe]
        public static BigInteger[] MatchOrder(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price, BigInteger amount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0 && amount > 0, "Invalid Parameters");
            var marketPrice = isBuy ? GetBuyPrice(pairKey) : GetSellPrice(pairKey);

            return MatchOrderInternal(pairKey, isBuy, marketPrice, price, amount);
        }

        [Safe]
        public static BigInteger[] MatchOrderAtPrice(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price, BigInteger amount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0 && amount > 0, "Invalid Parameters");

            return MatchOrderInternal(pairKey, isBuy, price, price, amount);
        }

        private static BigInteger[] MatchOrderInternal(byte[] pairKey, bool isBuy, BigInteger startPrice, BigInteger endPrice, BigInteger amount)
        {
            BigInteger totalPayment = 0;
            if (GetFirstOrderID(pairKey, !isBuy) is null) return new BigInteger[] { amount, totalPayment };

            var quoteDecimals = GetQuoteDecimals(pairKey);
            var currentOrder = GetFirstOrder(pairKey, !isBuy);

            while((isBuy && currentOrder.price < startPrice) || (!isBuy && currentOrder.price > startPrice))
            {
                if (currentOrder.nextID is null) return new BigInteger[] { amount, totalPayment };
                currentOrder = GetOrder(currentOrder.nextID);
            }

            while (amount > 0)
            {
                // Check price
                if ((isBuy && currentOrder.price > endPrice) || (!isBuy && currentOrder.price < endPrice)) break;

                if (currentOrder.amount <= amount) 
                {
                    totalPayment += currentOrder.amount * currentOrder.price / BigInteger.Pow(10, quoteDecimals);
                    amount -= currentOrder.amount;
                }
                else
                {
                    totalPayment += amount * currentOrder.price / BigInteger.Pow(10, quoteDecimals);
                    amount = 0;
                }

                if (currentOrder.nextID is null) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return new BigInteger[] { amount, totalPayment };
        }

        /// <summary>
        /// Try to match without real payment
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="quoteAmount"></param>
        /// <returns>Left amount and tradable base</returns>
        [Safe]
        public static BigInteger[] MatchQuote(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price, BigInteger quoteAmount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0 && quoteAmount > 0, "Invalid Parameters");
            var marketPrice = isBuy ? GetBuyPrice(pairKey) : GetSellPrice(pairKey);

            return MatchQuoteInternal(pairKey, isBuy, marketPrice, price, quoteAmount);
        }

        [Safe]
        public static BigInteger[] MatchQuoteAtPrice(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price, BigInteger quoteAmount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0 && quoteAmount > 0, "Invalid Parameters");

            return MatchQuoteInternal(pairKey, isBuy, price, price, quoteAmount);
        }

        private static BigInteger[] MatchQuoteInternal(byte[] pairKey, bool isBuy, BigInteger startPrice, BigInteger endPrice, BigInteger quoteAmount)
        {
            BigInteger totalTradable = 0;
            if (GetFirstOrderID(pairKey, !isBuy) is null) return new BigInteger[] { quoteAmount, totalTradable };

            var quoteDecimals = GetQuoteDecimals(pairKey);
            var currentOrder = GetFirstOrder(pairKey, !isBuy);

            while((isBuy && currentOrder.price < startPrice) || (!isBuy && currentOrder.price > startPrice))
            {
                if (currentOrder.nextID is null) return new BigInteger[] { quoteAmount, totalTradable };
                currentOrder = GetOrder(currentOrder.nextID);
            }

            while (quoteAmount > 0)
            {
                // Check price
                if ((isBuy && currentOrder.price > endPrice) || (!isBuy && currentOrder.price < endPrice)) break;

                var payment = currentOrder.amount * currentOrder.price / BigInteger.Pow(10, quoteDecimals);
                if (payment <= quoteAmount) 
                {
                    totalTradable += currentOrder.amount;
                    quoteAmount -= payment;
                }
                else
                {
                    // For buyer, real payment <= expected
                    if (isBuy) totalTradable += quoteAmount * BigInteger.Pow(10, quoteDecimals) / currentOrder.price;
                    // For seller, real payment >= expected
                    else totalTradable += (quoteAmount * BigInteger.Pow(10, quoteDecimals) + currentOrder.price - 1) / currentOrder.price;
                    quoteAmount = 0;
                }

                if (currentOrder.nextID is null) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return new BigInteger[] { quoteAmount, totalTradable };
        }

        /// <summary>
        /// Try to make a market deal with orderbook
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="taker"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns>Left amount</returns>
        public static BigInteger DealMarketOrder(UInt160 tokenA, UInt160 tokenB, UInt160 taker, bool isBuy, BigInteger price, BigInteger amount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0 && amount > 0, "Invalid Parameters");
            Assert(Runtime.CheckWitness(taker), "No Authorization");

            return DealMarketOrderInternal(pairKey, taker, isBuy, price, amount);
        }

        private static BigInteger DealMarketOrderInternal(byte[] pairKey, UInt160 taker, bool isBuy, BigInteger price, BigInteger amount)
        {
            if (GetFirstOrderID(pairKey, !isBuy) is null) return amount;

            var firstOrder = GetFirstOrder(pairKey, !isBuy);
            var canDeal = (isBuy && firstOrder.price <= price) || (!isBuy && firstOrder.price >= price);
            if (!canDeal) return amount;

            // Charge before settlement
            var me = Runtime.ExecutingScriptHash;
            var marketPrice = isBuy ? GetBuyPrice(pairKey) : GetSellPrice(pairKey);
            var matchResult = MatchOrderInternal(pairKey, isBuy, marketPrice, price, amount);
            if (isBuy) SafeTransfer(GetQuoteToken(pairKey), taker, me, matchResult[1]);
            else SafeTransfer(GetBaseToken(pairKey), taker, me, amount - matchResult[0]);

            return ExecuteDeal(pairKey, taker, isBuy, price, amount);
        }

        /// <summary>
        /// Settle order based on expected price
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="taker"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns>Left amount</returns>
        private static BigInteger ExecuteDeal(byte[] pairKey, UInt160 taker, bool isBuy, BigInteger price, BigInteger amount)
        {
            var leftAmount = amount;
            var me = Runtime.ExecutingScriptHash;
            var bookInfo = GetOrderBook(pairKey);
            var fundAddress = GetFundAddress();

            while (leftAmount > 0)
            {
                // Check if tradable
                if ((GetFirstOrderID(pairKey, !isBuy) is null)) break;
                // Check the lowest sell price
                if ((isBuy && GetBuyPrice(pairKey) > price) || (!isBuy && GetSellPrice(pairKey) < price)) break;

                var firstID = GetFirstOrderID(pairKey, !isBuy);
                var firstOrder = GetOrder(firstID);
                BigInteger quoteAmount = 0;
                BigInteger baseAmount = 0;
                BigInteger quotePayment = 0;
                BigInteger basePayment = 0;

                if (firstOrder.amount <= leftAmount)
                {
                    // Full-fill
                    quoteAmount = firstOrder.amount * firstOrder.price / BigInteger.Pow(10, (int)bookInfo.quoteDecimals);
                    baseAmount = firstOrder.amount;

                    // Remove full-fill order
                    RemoveFirstOrder(pairKey, !isBuy);
                    DeleteReceipt(firstOrder.maker, firstID);

                    onOrderStatusChanged(bookInfo.baseToken, bookInfo.quoteToken, firstID, isBuy, firstOrder.maker, firstOrder.price, 0);
                    leftAmount -= firstOrder.amount;
                }
                else
                {
                    // Part-fill
                    quoteAmount = leftAmount * firstOrder.price / BigInteger.Pow(10, (int)bookInfo.quoteDecimals);
                    baseAmount = leftAmount;

                    // Update order
                    firstOrder.amount -= leftAmount;
                    SetOrder(firstID, firstOrder);

                    onOrderStatusChanged(bookInfo.baseToken, bookInfo.quoteToken, firstID, isBuy, firstOrder.maker, firstOrder.price, firstOrder.amount);
                    leftAmount = 0;
                }

                // Do transfer
                quotePayment = quoteAmount * 9985 / 10000;
                basePayment = baseAmount * 9985 / 10000;
                if (isBuy)
                {
                    SafeTransfer(bookInfo.quoteToken, me, firstOrder.maker, quotePayment);
                    SafeTransfer(bookInfo.baseToken, me, taker, basePayment);
                }
                else
                {
                    SafeTransfer(bookInfo.baseToken, me, firstOrder.maker, basePayment);
                    SafeTransfer(bookInfo.quoteToken, me, taker, quotePayment);
                }
                if (fundAddress is not null)
                {
                    SafeTransfer(bookInfo.quoteToken, me, fundAddress, quoteAmount - quotePayment);
                    SafeTransfer(bookInfo.baseToken, me, fundAddress, baseAmount - basePayment);
                }
            }
            return leftAmount;
        }

        /// <summary>
        /// Internal price reporter
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        [Safe]
        public static BigInteger GetMarketPrice(UInt160 tokenA, UInt160 tokenB, bool isBuy)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            return isBuy ? GetBuyPrice(pairKey) : GetSellPrice(pairKey);
        }

        [Safe]
        public static BigInteger GetNextPrice(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0, "Invalid Parameters");

            return isBuy ? GetNextBuyPrice(pairKey, price) : GetNextSellPrice(pairKey, price);
        }

        /// <summary>
        /// Get book detail
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        [Safe]
        public static UInt160 GetBaseToken(UInt160 tokenA, UInt160 tokenB)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            return GetBaseToken(pairKey);
        }

        [Safe]
        public static UInt160 GetQuoteToken(UInt160 tokenA, UInt160 tokenB)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            return GetQuoteToken(pairKey);
        }

        [Safe]
        public static int GetQuoteDecimals(UInt160 tokenA, UInt160 tokenB)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            return GetQuoteDecimals(pairKey);
        }

        /// <summary>
        /// Get the lowest price to buy in orderbook
        /// </summary>
        /// <param name="pairKey"></param>
        /// <returns></returns>
        private static BigInteger GetBuyPrice(byte[] pairKey)
        {
            if (GetFirstOrderID(pairKey, false) is null) return 0;

            var firstSellOrder = GetFirstOrder(pairKey, false);
            return firstSellOrder.price;
        }

        private static BigInteger GetNextBuyPrice(byte[] pairKey, BigInteger price)
        {
            if (GetFirstOrderID(pairKey, false) is null) return 0;
            var currentSellOrder = GetFirstOrder(pairKey, false);

            while (currentSellOrder.price <= price)
            {
                if (currentSellOrder.nextID is null) return 0;
                currentSellOrder = GetOrder(currentSellOrder.nextID);
            }
            return currentSellOrder.price;
        }

        /// <summary>
        /// Get the highest price to sell in orderbook
        /// </summary>
        /// <param name="pairKey"></param>
        /// <returns></returns>
        private static BigInteger GetSellPrice(byte[] pairKey)
        {
            if (GetFirstOrderID(pairKey, true) is null) return 0;

            var firstBuyOrder = GetFirstOrder(pairKey, true);
            return firstBuyOrder.price;
        }

        private static BigInteger GetNextSellPrice(byte[] pairKey, BigInteger price)
        {
            if (GetFirstOrderID(pairKey, true) is null) return 0;
            var currentBuyOrder = GetFirstOrder(pairKey, true);

            while (currentBuyOrder.price >= price)
            {
                if (currentBuyOrder.nextID is null) return 0;
                currentBuyOrder = GetOrder(currentBuyOrder.nextID);
            }
            return currentBuyOrder.price;
        }
        #endregion

        #region AMM like API
        /// <summary>
        /// Calculate amountOut with amountIn
        /// </summary>
        /// <param name="tokenFrom"></param>
        /// <param name="tokenTo"></param>
        /// <param name="price"></param>
        /// <param name="amountIn"></param>
        /// <returns>Unsatisfied amountIn and amountOut</returns>
        [Safe]
        public static BigInteger[] GetAmountOut(UInt160 tokenFrom, UInt160 tokenTo, BigInteger startPrice, BigInteger endPrice, BigInteger amountIn)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(startPrice > 0 && endPrice > 0 && amountIn > 0, "Invalid Parameters");

            var isBuy = tokenFrom == GetQuoteToken(pairKey);
            if (isBuy)
            {
                var result = MatchQuoteInternal(pairKey, isBuy, startPrice, endPrice, amountIn);
                return new BigInteger[]{ result[0], result[1] * 9985 / 10000 };   // 0.15% fee
            }
            else
            {
                var result = MatchOrderInternal(pairKey, isBuy, startPrice, endPrice, amountIn);
                return new BigInteger[]{ result[0], result[1] * 9985 / 10000 };   // 0.15% fee
            }
        }

        /// <summary>
        /// Calculate amountOut with amountIn
        /// </summary>
        /// <param name="tokenFrom"></param>
        /// <param name="tokenTo"></param>
        /// <param name="price"></param>
        /// <param name="amountOut"></param>
        /// <returns>Unsatisfied amountOut and amountIn</returns>
        [Safe]
        public static BigInteger[] GetAmountIn(UInt160 tokenFrom, UInt160 tokenTo, BigInteger startPrice, BigInteger endPrice, BigInteger amountOut)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(startPrice > 0 && endPrice > 0 && amountOut > 0, "Invalid Parameters");

            var isBuy = tokenFrom == GetQuoteToken(pairKey);
            if (isBuy)
            {
                var amountIn = MatchOrderInternal(pairKey, isBuy, startPrice, endPrice, (amountOut * 10000 + 9984) / 9985)[1];   // 0.15% fee
                var leftOut = amountOut - MatchQuoteInternal(pairKey, isBuy, startPrice, endPrice, amountIn)[1] * 9985 / 10000;
                return new BigInteger[]{ leftOut, amountIn };
            }
            else
            {
                var amountIn = MatchQuoteInternal(pairKey, isBuy, startPrice, endPrice, (amountOut * 10000 + 9984) / 9985)[1];   // 0.15% fee
                var leftOut = amountOut - MatchOrderInternal(pairKey, isBuy, startPrice, endPrice, amountIn)[1] * 9985 / 10000;
                return new BigInteger[]{ leftOut, amountIn };
            }
        }
        #endregion

        public static void OnNEP17Payment(UInt160 sender, BigInteger amountIn, object data)
        {

        }
    }
}
