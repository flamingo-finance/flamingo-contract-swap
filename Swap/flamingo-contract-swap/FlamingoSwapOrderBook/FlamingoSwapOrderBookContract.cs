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
            public UInt160 sender;
            public BigInteger price;
            public BigInteger amount;
            public uint nextID;
        }

        public struct Orderbook
        {
            public UInt160 baseToken;
            public UInt160 quoteToken;
            public uint quoteDecimals;

            public uint firstBuyID;
            public uint firstSellID;
        }

        const uint MAX_ID = 1 << 24;

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
            SetOrderbook(pairKey, new Orderbook(){
                baseToken = baseToken,
                quoteToken = quoteToken,
                quoteDecimals = quoteDecimals
            });
            onRegisterBook(baseToken, quoteToken, quoteDecimals);
            return true;
        }

        // public static bool RemoveOrderBook(UInt160 baseToken, UInt160 quoteToken)
        // {
        //     Assert(baseToken.IsAddress() && quoteToken.IsAddress(), "Invalid Address");
        //     Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");

        //     var pairKey = GetPairKey(baseToken, quoteToken);
        //     if (!BookExists(pairKey)) return false;
        //     DeleteOrderBook(pairKey);
        //     onRemoveBook(baseToken, quoteToken);
        //     return true;
        // }

        /// <summary>
        /// Add a new order into orderbook but try deal it first
        /// </summary>
        /// <param name="tokenFrom"></param>
        /// <param name="tokenTo"></param>
        /// <param name="sender"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static uint DealLimitOrder(UInt160 tokenFrom, UInt160 tokenTo, UInt160 sender, BigInteger price, BigInteger amount)
        {
            // Deal as market order
            BigInteger leftAmount = DealMarketOrder(tokenFrom, tokenTo, sender, price, amount);

            // Deposit token
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            bool isBuy = tokenFrom == GetQuoteToken(pairKey);
            UInt160 me = Runtime.ExecutingScriptHash;
            if (isBuy) SafeTransfer(GetQuoteToken(pairKey), sender, me, leftAmount * price / BigInteger.Pow(10, GetQuoteDecimals(pairKey)));
            else SafeTransfer(GetBaseToken(pairKey), sender, me, leftAmount);

            // Do add
            uint id = GetUnusedID();
            LimitOrder order = new LimitOrder()
            {
                sender = sender,
                price = price,
                amount = leftAmount
            };
            Assert(InsertOrder(pairKey, id, order, isBuy), "Add Order Fail");
            onAddOrder(tokenFrom, tokenTo, sender, price, leftAmount);
            return id;
        }

        /// <summary>
        /// Cancel a limit order from orderbook
        /// </summary>
        /// <param name="tokenFrom"></param>
        /// <param name="tokenTo"></param>
        /// <param name="id"></param>
        public static void CancelOrder(UInt160 tokenFrom, UInt160 tokenTo, uint id)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(OrderExists(id), "Order Not Exists");
            LimitOrder order = GetOrder(id);
            Assert(Runtime.CheckWitness(order.sender), "No Authorization");

            // Do remove
            bool isBuy = tokenFrom == GetQuoteToken(pairKey);
            Assert(RemoveOrder(pairKey, id, isBuy), "Remove Order Fail");
            onCancelOrder(id, order.amount);

            // Withdraw token
            UInt160 me = Runtime.ExecutingScriptHash;
            if (isBuy) SafeTransfer(GetQuoteToken(pairKey), me, order.sender, order.amount * order.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey)));
            else SafeTransfer(GetBaseToken(pairKey), me, order.sender, order.amount);
        }

        /// <summary>
        /// Get first N limit orders and their details
        /// </summary>
        /// <param name="tokenFrom"></param>
        /// <param name="tokenTo"></param>
        /// <param name="id"></param>
        public static LimitOrder[] GetFirstNOrders(UInt160 tokenFrom, UInt160 tokenTo, uint n)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            Assert(BookExists(pairKey), "Book Not Exists");

            LimitOrder[] results = new LimitOrder[n];
            bool isBuy = tokenFrom == GetQuoteToken(pairKey);
            if (GetFirstOrderID(pairKey, isBuy) == 0) return results;
            LimitOrder currentOrder = GetFirstOrder(pairKey, isBuy);
            for (int i = 0; i < n; i++)
            {
                results[i] = currentOrder;
                if (currentOrder.nextID == 0) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return results;
        }

        /// <summary>
        /// Try to match without real payment
        /// </summary>
        /// <param name="tokenFrom"></param>
        /// <param name="tokenTo"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns>Left amount and total price</returns>
        public static BigInteger[] MatchOrder(UInt160 tokenFrom, UInt160 tokenTo, BigInteger price, BigInteger amount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            Assert(BookExists(pairKey), "Book Not Exists");

            bool isBuy = tokenFrom == GetQuoteToken(pairKey);
            return isBuy ? MatchBuy(pairKey, price, amount) : MatchSell(pairKey, price, amount);
        }

        /// <summary>
        /// Try to buy without real payment
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns>Left amount and total price</returns>
        private static BigInteger[] MatchBuy(byte[] pairKey, BigInteger price, BigInteger amount)
        {
            BigInteger totalPayment = 0;
            if (GetFirstOrderID(pairKey, false) == 0) return new BigInteger[] { amount, totalPayment };
            LimitOrder currentOrder = GetFirstOrder(pairKey, false);

            while (amount > 0)
            {
                // Check sell price
                if (currentOrder.price > price) break;

                if (currentOrder.amount <= amount) 
                {
                    totalPayment += currentOrder.amount * currentOrder.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey));
                    amount -= currentOrder.amount;
                }
                else
                {
                    totalPayment += currentOrder.amount * currentOrder.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey));
                    amount = 0;
                }

                if (currentOrder.nextID == 0) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return new BigInteger[] { amount, totalPayment };
        }

        /// <summary>
        /// Try to sell without real payment
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns>Left amount and total price</returns>
        private static BigInteger[] MatchSell(byte[] pairKey, BigInteger price, BigInteger amount)
        {
            BigInteger totalPayment = 0;
            if (GetFirstOrderID(pairKey, true) == 0) return new BigInteger[] { amount, totalPayment };
            LimitOrder currentOrder = GetFirstOrder(pairKey, true);

            while (amount > 0)
            {
                // Check buy price
                if (currentOrder.price < price) break;

                if (currentOrder.amount <= amount)
                {
                    totalPayment += currentOrder.amount * currentOrder.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey));
                    amount -= currentOrder.amount;
                }
                else
                {
                    totalPayment += amount * currentOrder.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey));
                    amount = 0;
                }

                if (currentOrder.nextID == 0) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return new BigInteger[] { amount, totalPayment };
        }

        /// <summary>
        /// Calculate how much quote token should be paid when buy
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private static BigInteger GetQuoteAmount(byte[] pairKey, BigInteger price, BigInteger amount)
        {
            BigInteger result = 0;
            if (GetFirstOrderID(pairKey, false) == 0) return result;
            LimitOrder currentOrder = GetFirstOrder(pairKey, false);
            while (amount > 0)
            {
                // Check sell price
                if (currentOrder.price > price) break;

                // Full-fill
                if (currentOrder.amount <= amount)
                {
                    result += currentOrder.amount * currentOrder.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey));
                    amount -= currentOrder.amount;
                }
                // Part-fill
                else 
                {
                    result += amount * currentOrder.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey));
                    amount = 0;
                }

                if (currentOrder.nextID == 0) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return result;
        }

        /// <summary>
        /// Calculate how much base token should be paid when sell
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private static BigInteger GetBaseAmount(byte[] pairKey, BigInteger price, BigInteger amount)
        {
            BigInteger result = 0;
            if (GetFirstOrderID(pairKey, true) == 0) return result;
            LimitOrder currentOrder = GetFirstOrder(pairKey, true);
            while (amount > 0)
            {
                // Check buy price
                if (currentOrder.price < price) break;

                // Full-fill
                if (currentOrder.amount <= amount)
                {
                    result += currentOrder.amount;
                    amount -= currentOrder.amount;
                }
                // Part-fill
                else 
                {
                    result += amount;
                    amount = 0;
                }

                if (currentOrder.nextID == 0) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return result;
        }

        /// <summary>
        /// Try to make a market deal with orderbook
        /// </summary>
        /// <param name="tokenFrom"></param>
        /// <param name="tokenTo"></param>
        /// <param name="sender"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static BigInteger DealMarketOrder(UInt160 tokenFrom, UInt160 tokenTo, UInt160 sender, BigInteger price, BigInteger amount)
        {
            // Check if can deal
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            Assert(BookExists(pairKey), "Book Not Exists");
            bool isBuy = tokenFrom == GetQuoteToken(pairKey);
            if (GetFirstOrderID(pairKey, !isBuy) == 0) return amount;

            LimitOrder firstOrder = GetFirstOrder(pairKey, !isBuy);
            bool canDeal = (isBuy && firstOrder.price <= price) || (!isBuy && firstOrder.price >= price);
            if (!canDeal) return amount;

            // Do deal
            UInt160 me = Runtime.ExecutingScriptHash;
            Assert(Runtime.CheckWitness(sender), "No Authorization");
            if (isBuy)
            {
                BigInteger quoteAmount = GetQuoteAmount(pairKey, price, amount);
                SafeTransfer(GetQuoteToken(pairKey), sender, me, quoteAmount);
                return DealBuy(pairKey, sender, price, amount);
            }
            else
            {
                BigInteger baseAmount = GetBaseAmount(pairKey, price, amount);
                SafeTransfer(GetBaseToken(pairKey), sender, me, baseAmount);
                return DealSell(pairKey, sender, price, amount);
            }
        }

        /// <summary>
        /// Buy below the expected price
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="buyer"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private static BigInteger DealBuy(byte[] pairKey, UInt160 buyer, BigInteger price, BigInteger amount)
        {
            UInt160 me = Runtime.ExecutingScriptHash;
            while (amount > 0 && GetFirstOrderID(pairKey, false) != 0)
            {
                // Check the lowest sell price
                if (GetBuyPrice(pairKey) > price) break;

                LimitOrder firstOrder = GetFirstOrder(pairKey, false);
                if (firstOrder.amount <= amount)
                {
                    // Full-fill
                    amount -= firstOrder.amount;
                    // Do transfer
                    SafeTransfer(GetQuoteToken(pairKey), me, firstOrder.sender, firstOrder.amount * firstOrder.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey)));
                    SafeTransfer(GetBaseToken(pairKey), me, buyer, firstOrder.amount);
                    onDealOrder(GetFirstOrderID(pairKey, false), firstOrder.price, firstOrder.amount, 0);
                    // Remove full-fill order
                    RemoveFirstOrder(pairKey, false);
                }
                else
                {
                    // Part-fill
                    firstOrder.amount -= amount;
                    // Do transfer
                    SafeTransfer(GetQuoteToken(pairKey), me, firstOrder.sender, amount * firstOrder.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey)));
                    SafeTransfer(GetBaseToken(pairKey), me, buyer, amount);
                    onDealOrder(GetFirstOrderID(pairKey, false), firstOrder.price, amount, firstOrder.amount);
                    // Update order
                    SetOrder(GetFirstOrderID(pairKey, false), firstOrder);
                    amount = 0;
                }
            }
            return amount;
        }

        /// <summary>
        /// Sell above the expected price
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="seller"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private static BigInteger DealSell(byte[] pairKey, UInt160 seller, BigInteger price, BigInteger amount)
        {
            UInt160 me = Runtime.ExecutingScriptHash;
            while (amount > 0 && GetFirstOrderID(pairKey, true) != 0)
            {
                // Check the highest buy price
                if (GetSellPrice(pairKey) < price) break;

                LimitOrder firstOrder = GetFirstOrder(pairKey, true);
                if (firstOrder.amount <= amount)
                {
                    // Full-fill
                    amount -= firstOrder.amount;
                    // Do transfer
                    SafeTransfer(GetBaseToken(pairKey), me, firstOrder.sender, firstOrder.amount);
                    SafeTransfer(GetQuoteToken(pairKey), me, seller, firstOrder.amount * firstOrder.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey)));
                    onDealOrder(GetFirstOrderID(pairKey, true), firstOrder.price, firstOrder.amount, 0);
                    // Remove full-fill order
                    RemoveFirstOrder(pairKey, true);
                }
                else
                {
                    // Part-fill
                    firstOrder.amount -= amount;
                    // Do transfer
                    SafeTransfer(GetBaseToken(pairKey), me, firstOrder.sender, amount);
                    SafeTransfer(GetQuoteToken(pairKey), me, seller, amount * firstOrder.price / BigInteger.Pow(10, GetQuoteDecimals(pairKey)));
                    onDealOrder(GetFirstOrderID(pairKey, true), firstOrder.price, amount, firstOrder.amount);
                    // Update order
                    SetOrder(GetFirstOrderID(pairKey, true), firstOrder);
                    amount = 0;
                }
            }
            return amount;
        }

        /// <summary>
        /// Internal price reporter
        /// </summary>
        /// <param name="tokenFrom"></param>
        /// <param name="tokenTo"></param>
        /// <returns></returns>
        public static BigInteger GetMarketPrice(UInt160 tokenFrom, UInt160 tokenTo)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            Assert(BookExists(pairKey), "Book Not Exists");

            bool isBuy = tokenFrom == GetQuoteToken(pairKey);
            return isBuy ? GetBuyPrice(pairKey) : GetSellPrice(pairKey);
        }

        /// <summary>
        /// Get the lowest price to buy in orderbook
        /// </summary>
        /// <param name="pairKey"></param>
        /// <returns></returns>
        private static BigInteger GetBuyPrice(byte[] pairKey)
        {
            if (GetFirstOrderID(pairKey, false) == 0) return 0;

            LimitOrder firstSellOrder = GetFirstOrder(pairKey, false);
            return firstSellOrder.price;
        }

        /// <summary>
        /// Get the highest price to sell in orderbook
        /// </summary>
        /// <param name="pairKey"></param>
        /// <returns></returns>
        private static BigInteger GetSellPrice(byte[] pairKey)
        {
            if (GetFirstOrderID(pairKey, true) == 0) return 0;

            LimitOrder firstBuyOrder = GetFirstOrder(pairKey, true);
            return firstBuyOrder.price;
        }

        public static void OnNEP17Payment(UInt160 sender, BigInteger amountIn, object data)
        {

        }
    }
}
