using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
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
            public BigInteger minOrderAmount;
            public BigInteger maxOrderAmount;

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
        /// <param name="minOrderAmount"></param>
        /// <param name="maxOrderAmount"></param>
        /// <returns></returns>
        public static bool RegisterOrderBook(UInt160 baseToken, UInt160 quoteToken, uint quoteDecimals, BigInteger minOrderAmount, BigInteger maxOrderAmount)
        {
            Assert(baseToken.IsAddress() && quoteToken.IsAddress(), "Invalid Address");
            Assert(baseToken != quoteToken, "Invalid Trade Pair");
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            if (BookExists(pairKey)) return false;
            SetOrderBook(pairKey, new OrderBook(){
                baseToken = baseToken,
                quoteToken = quoteToken,
                quoteDecimals = quoteDecimals,
                minOrderAmount = minOrderAmount,
                maxOrderAmount = maxOrderAmount
            });
            onRegisterBook(baseToken, quoteToken, quoteDecimals, minOrderAmount, maxOrderAmount);
            return true;
        }

        /// <summary>
        /// Set the minimum order amount for addLimitOrder
        /// </summary>
        /// <param name="baseToken"></param>
        /// <param name="quoteToken"></param>
        /// <param name="minOrderAmount"></param>
        /// <returns></returns>
        public static bool SetMinOrderAmount(UInt160 baseToken, UInt160 quoteToken, BigInteger minOrderAmount)
        {
            Assert(baseToken.IsAddress() && quoteToken.IsAddress(), "Invalid Address");
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            if (!BookExists(pairKey)) return false;
            if (GetBaseToken(pairKey) != baseToken) return false;
            if (GetQuoteToken(pairKey) != quoteToken) return false;

            var book = GetOrderBook(pairKey);
            book.minOrderAmount = minOrderAmount;
            SetOrderBook(pairKey, book);
            return true;
        }

        /// <summary>
        /// Set the maximum trade amount for addLimitOrder
        /// </summary>
        /// <param name="baseToken"></param>
        /// <param name="quoteToken"></param>
        /// <param name="maxOrderAmount"></param>
        /// <returns></returns>
        public static bool SetMaxOrderAmount(UInt160 baseToken, UInt160 quoteToken, BigInteger maxOrderAmount)
        {
            Assert(baseToken.IsAddress() && quoteToken.IsAddress(), "Invalid Address");
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            if (!BookExists(pairKey)) return false;
            if (GetBaseToken(pairKey) != baseToken) return false;
            if (GetQuoteToken(pairKey) != quoteToken) return false;

            var book = GetOrderBook(pairKey);
            book.maxOrderAmount = maxOrderAmount;
            SetOrderBook(pairKey, book);
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
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            if (!BookExists(pairKey)) return false;
            if (GetBaseToken(pairKey) != baseToken) return false;
            if (GetQuoteToken(pairKey) != quoteToken) return false;

            var totalQuotePayment = new Map<UInt160, BigInteger>();
            var totalBasePayment = new Map<UInt160, BigInteger>();

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

                // Record payment
                var quoteAmount = order.amount * order.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey));
                if (totalQuotePayment.HasKey(order.maker)) totalQuotePayment[order.maker] += quoteAmount;
                else totalQuotePayment[order.maker] = quoteAmount;

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

                // Record payment
                if (totalBasePayment.HasKey(order.maker)) totalBasePayment[order.maker] += order.amount;
                else totalBasePayment[order.maker] = order.amount;

                // Try again
                firstSellID = GetFirstOrderID(pairKey, false);
            }

            // Remove book
            DeleteOrderBook(pairKey);
            onRemoveBook(baseToken, quoteToken);

            // Do transfer
            var me = Runtime.ExecutingScriptHash;
            foreach (var maker in totalQuotePayment.Keys)
            {
                SafeTransfer(quoteToken, me, maker, totalQuotePayment[maker]);
            }
            foreach (var maker in totalBasePayment.Keys)
            {
                SafeTransfer(baseToken, me, maker, totalBasePayment[maker]);
            }

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

            // Check parameters
            Assert(price > 0 && amount > 0, "Invalid Parameters");

            // Check maker
            Assert(Runtime.CheckWitness(maker), "No Authorization");
            Assert(ContractManagement.GetContract(maker) == null, "Forbidden");

            // Deal as market order
            var leftAmount = DealMarketOrderInternal(pairKey, maker, isBuy, price, amount);
            if (leftAmount == 0) return null;
            if (leftAmount < GetMinOrderAmount(pairKey)) return null;
            if (leftAmount > GetMaxOrderAmount(pairKey)) return null;

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
        public static BigInteger[] MatchOrder(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price, BigInteger amount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0 && amount > 0, "Invalid Parameters");
            var marketPrice = isBuy ? GetBuyPrice(pairKey) : GetSellPrice(pairKey);

            return MatchOrderInternal(pairKey, isBuy, marketPrice, price, amount);
        }

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
        public static BigInteger[] MatchQuote(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price, BigInteger quoteAmount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0 && quoteAmount > 0, "Invalid Parameters");
            var marketPrice = isBuy ? GetBuyPrice(pairKey) : GetSellPrice(pairKey);

            return MatchQuoteInternal(pairKey, isBuy, marketPrice, price, quoteAmount);
        }

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

            // Check parameters
            Assert(price > 0 && amount > 0, "Invalid Parameters");

            // Check taker
            Assert(Runtime.CheckWitness(taker), "No Authorization");
            Assert(ContractManagement.GetContract(taker) == null, "Forbidden");

            return DealMarketOrderInternal(pairKey, taker, isBuy, price, amount);
        }

        public static BigInteger DealMarketOrder(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price, BigInteger amount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            // Check parameters
            Assert(price > 0 && amount > 0, "Invalid Parameters");

            // Check taker
            var caller = Runtime.CallingScriptHash;
            Assert(ContractManagement.GetContract(caller) != null, "Forbidden"); 

            return DealMarketOrderInternal(pairKey, caller, isBuy, price, amount);
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

            if (ContractManagement.GetContract(taker) != null)
            {
                if (isBuy) RequestTransfer(GetQuoteToken(pairKey), taker, me, matchResult[1]);
                else RequestTransfer(GetBaseToken(pairKey), taker, me, amount - matchResult[0]);
            }
            else
            {
                if (isBuy) SafeTransfer(GetQuoteToken(pairKey), taker, me, matchResult[1]);
                else SafeTransfer(GetBaseToken(pairKey), taker, me, amount - matchResult[0]);
            }

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

            BigInteger quoteFee = 0;
            BigInteger baseFee = 0;

            var totalQuotePayment = new Map<UInt160, BigInteger>();
            var totalBasePayment = new Map<UInt160, BigInteger>();

            while (leftAmount > 0)
            {
                // Check if tradable
                if ((GetFirstOrderID(pairKey, !isBuy) is null)) break;
                // Check the lowest sell price
                if ((isBuy && GetBuyPrice(pairKey) > price) || (!isBuy && GetSellPrice(pairKey) < price)) break;

                var firstID = GetFirstOrderID(pairKey, !isBuy);
                var firstOrder = GetOrder(firstID);
                BigInteger quoteAmount;
                BigInteger baseAmount;
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

                // Record payment
                quotePayment = quoteAmount * 9985 / 10000;
                basePayment = baseAmount * 9985 / 10000;

                if (isBuy)
                {
                    if (totalQuotePayment.HasKey(firstOrder.maker)) totalQuotePayment[firstOrder.maker] += quotePayment;
                    else totalQuotePayment[firstOrder.maker] = quotePayment;
                    if (totalBasePayment.HasKey(taker)) totalBasePayment[taker] += basePayment;
                    else totalBasePayment[taker] = basePayment;
                }
                else
                {
                    if (totalQuotePayment.HasKey(taker)) totalQuotePayment[taker] += quotePayment;
                    else totalQuotePayment[taker] = quotePayment;
                    if (totalBasePayment.HasKey(firstOrder.maker)) totalBasePayment[firstOrder.maker] += basePayment;
                    else totalBasePayment[firstOrder.maker] = basePayment;
                }
                quoteFee += quoteAmount - quotePayment;
                baseFee += baseAmount - basePayment;
            }

            // Do transfer
            foreach (var toAddress in totalQuotePayment.Keys)
            {
                SafeTransfer(bookInfo.quoteToken, me, toAddress, totalQuotePayment[toAddress]);
            }
            foreach (var toAddress in totalBasePayment.Keys)
            {
                SafeTransfer(bookInfo.baseToken, me, toAddress, totalBasePayment[toAddress]);
            }

            if (fundAddress is not null)
            {
                SafeTransfer(bookInfo.quoteToken, me, fundAddress, quoteFee);
                SafeTransfer(bookInfo.baseToken, me, fundAddress, baseFee);
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
        public static BigInteger GetMarketPrice(UInt160 tokenA, UInt160 tokenB, bool isBuy)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            return isBuy ? GetBuyPrice(pairKey) : GetSellPrice(pairKey);
        }

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
        public static UInt160 GetBaseToken(UInt160 tokenA, UInt160 tokenB)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            return GetBaseToken(pairKey);
        }

        public static UInt160 GetQuoteToken(UInt160 tokenA, UInt160 tokenB)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            return GetQuoteToken(pairKey);
        }

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
