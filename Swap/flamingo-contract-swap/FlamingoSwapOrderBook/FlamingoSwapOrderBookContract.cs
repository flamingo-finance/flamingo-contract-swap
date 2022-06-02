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
                onCancelOrder(firstBuyID, order.price, order.amount);

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
                onCancelOrder(firstSellID, order.price, order.amount);

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
            onAddOrder(GetBaseToken(pairKey), GetQuoteToken(pairKey), id, isBuy, price, leftAmount);
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
            Assert(RemoveOrder(pairKey, id, isBuy), "Remove Order Fail");
            onCancelOrder(id, order.price, order.amount);

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
        /// <param name="id"></param>
        /// <returns></returns>
        [Safe]
        public static LimitOrder[] GetFirstNOrders(UInt160 tokenA, UInt160 tokenB, bool isBuy, uint n)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            var results = new LimitOrder[n];
            if (GetFirstOrderID(pairKey, isBuy) is null) return results;
            var currentOrder = GetFirstOrder(pairKey, isBuy);
            for (int i = 0; i < n; i++)
            {
                results[i] = currentOrder;
                if (currentOrder.nextID is null) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return results;
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

            return MatchOrderInternal(pairKey, isBuy, price, amount);
        }

        private static BigInteger[] MatchOrderInternal(byte[] pairKey, bool isBuy, BigInteger price, BigInteger amount)
        {
            BigInteger totalPayment = 0;
            if (GetFirstOrderID(pairKey, !isBuy) is null) return new BigInteger[] { amount, totalPayment };

            var quoteDecimals = GetQuoteDecimals(pairKey);
            var currentOrder = GetFirstOrder(pairKey, !isBuy);

            while (amount > 0)
            {
                // Check price
                if ((isBuy && currentOrder.price > price) || (!isBuy && currentOrder.price < price)) break;

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
            var matchResult = MatchOrderInternal(pairKey, isBuy, price, amount);
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
                BigInteger makerFee = 0;
                BigInteger takerFee = 0;

                if (firstOrder.amount <= leftAmount)
                {
                    // Full-fill
                    quoteAmount = firstOrder.amount * firstOrder.price / BigInteger.Pow(10, (int)bookInfo.quoteDecimals);
                    baseAmount = firstOrder.amount;

                    // Remove full-fill order
                    RemoveFirstOrder(pairKey, !isBuy);

                    onDealOrder(firstID, firstOrder.price, firstOrder.amount);
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

                    onDealOrder(firstID, firstOrder.price, leftAmount);
                    leftAmount = 0;
                }

                // Do transfer
                if (isBuy)
                {
                    makerFee = quoteAmount * 15 / 10000;
                    takerFee = baseAmount * 15 / 10000;
                    SafeTransfer(bookInfo.quoteToken, me, firstOrder.maker, quoteAmount - makerFee);
                    SafeTransfer(bookInfo.baseToken, me, taker, baseAmount - takerFee);
                    if (fundAddress is not null)
                    {
                        SafeTransfer(bookInfo.quoteToken, me, fundAddress, makerFee);
                        SafeTransfer(bookInfo.baseToken, me, fundAddress, takerFee);
                    }
                }
                else
                {
                    makerFee = baseAmount * 15 / 10000;
                    takerFee = quoteAmount * 15 / 10000;
                    SafeTransfer(bookInfo.baseToken, me, firstOrder.maker, baseAmount - makerFee);
                    SafeTransfer(bookInfo.quoteToken, me, taker, quoteAmount - takerFee);
                    if (fundAddress is not null)
                    {
                        SafeTransfer(bookInfo.quoteToken, me, fundAddress, takerFee);
                        SafeTransfer(bookInfo.baseToken, me, fundAddress, makerFee);
                    }
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
        public static BigInteger[] GetAmountOut(UInt160 tokenFrom, UInt160 tokenTo, BigInteger price, BigInteger amountIn)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0 && amountIn > 0, "Invalid Parameters");

            var isBuy = tokenFrom == GetQuoteToken(pairKey);
            if (isBuy)
            {
                var result = MatchQuoteInternal(pairKey, isBuy, price, amountIn);
                return new BigInteger[]{ result[0], result[1] - result[1] * 15 / 10000 };   // 0.15% fee
            }
            else
            {
                var result = MatchOrderInternal(pairKey, isBuy, price, amountIn);
                return new BigInteger[]{ result[0], result[1] - result[1] * 15 / 10000 };   // 0.15% fee
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
        public static BigInteger[] GetAmountIn(UInt160 tokenFrom, UInt160 tokenTo, BigInteger price, BigInteger amountOut)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0 && amountOut > 0, "Invalid Parameters");

            var isBuy = tokenFrom == GetQuoteToken(pairKey);
            if (isBuy)
            {
                var result = MatchOrderInternal(pairKey, isBuy, price, amountOut * 10000 / 9985);   // 0.15% fee
                return new BigInteger[]{ result[0], result[1] };
            }
            else
            {
                var result = MatchQuoteInternal(pairKey, isBuy, price, amountOut * 10000 / 9985);   // 0.15% fee
                return new BigInteger[]{ result[0], result[1] };
            }
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

            return MatchQuoteInternal(pairKey, isBuy, price, quoteAmount);
        }

        private static BigInteger[] MatchQuoteInternal(byte[] pairKey, bool isBuy, BigInteger price, BigInteger quoteAmount)
        {
            BigInteger totalTradable = 0;
            if (GetFirstOrderID(pairKey, !isBuy) is null) return new BigInteger[] { quoteAmount, totalTradable };

            var quoteDecimals = GetQuoteDecimals(pairKey);
            var currentOrder = GetFirstOrder(pairKey, !isBuy);

            while (quoteAmount > 0)
            {
                // Check price
                if ((isBuy && currentOrder.price > price) || (!isBuy && currentOrder.price < price)) break;

                var payment = currentOrder.amount * currentOrder.price / BigInteger.Pow(10, quoteDecimals);
                if (payment <= quoteAmount) 
                {
                    totalTradable += currentOrder.amount;
                    quoteAmount -= payment;
                }
                else
                {
                    totalTradable += quoteAmount * BigInteger.Pow(10, quoteDecimals) / currentOrder.price;
                    quoteAmount = 0;
                }

                if (currentOrder.nextID is null) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return new BigInteger[] { quoteAmount, totalTradable };
        }
        #endregion

        public static void OnNEP17Payment(UInt160 sender, BigInteger amountIn, object data)
        {

        }
    }
}
