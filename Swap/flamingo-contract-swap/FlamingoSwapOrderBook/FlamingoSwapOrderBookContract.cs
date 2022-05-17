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
            public uint firstBuyID;
            public uint firstSellID;
        }

        const uint MAX_ID = 1 << 24;

        /// <summary>
        /// Register a new book
        /// </summary>
        /// <param name="pair"></param>
        /// <returns></returns>
        public static bool RegisterOrderBook(UInt160 pair, UInt160 baseToken, UInt160 quoteToken)
        {
            if (BookExists(pair)) return false;
            SetOrderbook(pair, new Orderbook(){
                baseToken = baseToken,
                quoteToken = quoteToken
            });
            return true;
        }

        /// <summary>
        /// Add a new order into orderbook but try deal it first
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="sender"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        public static uint AddOrder(UInt160 pair, UInt160 sender, BigInteger price, BigInteger amount, bool isBuy)
        {
            // Check Authorization
            Assert(Runtime.CheckWitness(sender), "No Authorization");
            Assert(BookExists(pair), "Book Not Exists");

            // Try deal
            BigInteger leftAmount = DealOrder(pair, sender, price, amount, isBuy);

            // Deposit token
            UInt160 me = Runtime.ExecutingScriptHash;
            if (isBuy) SafeTransfer(GetQuoteToken(pair), sender, me, amount * price);
            else SafeTransfer(GetBaseToken(pair), sender, me, amount);

            // Do add
            uint id = GetUnusedID();
            LimitOrder order = new LimitOrder()
            {
                sender = sender,
                price = price,
                amount = leftAmount
            };
            Assert(InsertOrder(pair, id, order, isBuy), "Add Order Fail");
            return id;
        }

        /// <summary>
        /// Cancel a limit order from orderbook
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="id"></param>
        /// <param name="isBuy"></param>
        public static void CancelOrder(UInt160 pair, uint id, bool isBuy)
        {
            // Check if exist
            Assert(BookExists(pair), "Book Not Exists");
            Assert(OrderExists(id), "Order Not Exists");
            LimitOrder order = GetOrder(id);
            Assert(Runtime.CheckWitness(order.sender), "No Authorization");

            // Do remove
            Assert(RemoveOrder(pair, id, isBuy), "Remove Order Fail");

            // Withdraw token
            UInt160 me = Runtime.ExecutingScriptHash;
            if (isBuy) SafeTransfer(GetQuoteToken(pair), me, order.sender, order.amount * order.price);
            else SafeTransfer(GetBaseToken(pair), me, order.sender, order.amount);
        }

        /// <summary>
        /// Try to buy without real payment
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static BigInteger MatchBuy(UInt160 pair, BigInteger price, BigInteger amount)
        {
            Assert(BookExists(pair), "Book Not Exists");
            if (GetFirstOrderID(pair, false).Equals(0)) return amount;
            LimitOrder currentOrder = GetFirstOrder(pair, false);

            while (amount > 0)
            {
                // Check sell price
                if (currentOrder.price > price) break;

                if (currentOrder.amount <= amount) amount -= currentOrder.amount;
                else amount = 0;

                if (currentOrder.nextID == 0) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return amount;
        }

        /// <summary>
        /// Try to sell without real payment
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static BigInteger MatchSell(UInt160 pair, BigInteger price, BigInteger amount)
        {
            Assert(BookExists(pair), "Book Not Exists");
            if (GetFirstOrderID(pair, true).Equals(0)) return amount;
            LimitOrder currentOrder = GetFirstOrder(pair, true);

            while (amount > 0)
            {
                // Check buy price
                if (currentOrder.price < price) break;

                if (currentOrder.amount <= amount) amount -= currentOrder.amount;
                else amount = 0;

                if (currentOrder.nextID == 0) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return amount;
        }

        /// <summary>
        /// Calculate how much quote token should be paid
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static BigInteger GetQuoteAmount(UInt160 pair, BigInteger price, BigInteger amount)
        {
            Assert(BookExists(pair), "Book Not Exists");
            BigInteger result = 0;
            if (GetFirstOrderID(pair, false).Equals(0)) return result;
            LimitOrder currentOrder = GetFirstOrder(pair, false);
            while (amount > 0)
            {
                // Check sell price
                if (currentOrder.price > price) break;

                // Full-fill
                if (currentOrder.amount <= amount)
                {
                    result += currentOrder.amount * currentOrder.price;
                    amount -= currentOrder.amount;
                }
                // Part-fill
                else 
                {
                    result += amount * currentOrder.price;
                    amount = 0;
                }

                if (currentOrder.nextID == 0) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return result;
        }

        /// <summary>
        /// Calculate how much base token should be paid
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static BigInteger GetBaseAmount(UInt160 pair, BigInteger price, BigInteger amount)
        {
            Assert(BookExists(pair), "Book Not Exists");
            BigInteger result = 0;
            if (GetFirstOrderID(pair, true).Equals(0)) return result;
            LimitOrder currentOrder = GetFirstOrder(pair, true);
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
        /// Try to make the deal with orderbook
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="sender"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        public static BigInteger DealOrder(UInt160 pair, UInt160 sender, BigInteger price, BigInteger amount, bool isBuy)
        {
            // Check if can deal
            Assert(BookExists(pair), "Book Not Exists");
            if (GetFirstOrderID(pair, !isBuy).Equals(0)) return amount;

            LimitOrder firstOrder = GetFirstOrder(pair, !isBuy);
            bool canDeal = (isBuy && firstOrder.price <= price) || (!isBuy && firstOrder.price >= price);
            if (!canDeal) return amount;

            // Do deal
            UInt160 me = Runtime.ExecutingScriptHash;
            Assert(Runtime.CheckWitness(sender), "No Authorization");
            if (isBuy)
            {
                BigInteger quoteAmount = GetQuoteAmount(pair, price, amount);
                SafeTransfer(GetQuoteToken(pair), sender, me, quoteAmount);
                return DealBuy(pair, sender, price, amount);
            }
            else
            {
                BigInteger baseAmount = GetBaseAmount(pair, price, amount);
                SafeTransfer(GetBaseToken(pair), sender, me, baseAmount);
                return DealSell(pair, sender, price, amount);
            }
        }

        /// <summary>
        /// Get the lowest price to buy in orderbook
        /// </summary>
        /// <param name="pair"></param>
        /// <returns></returns>
        public static BigInteger GetBuyPrice(UInt160 pair)
        {
            Assert(BookExists(pair), "Book Not Exists");
            if (GetFirstOrderID(pair, false).Equals(0)) return 0;

            LimitOrder firstSellOrder = GetFirstOrder(pair, false);
            return firstSellOrder.price;
        }

        /// <summary>
        /// Get the highest price to sell in orderbook
        /// </summary>
        /// <param name="pair"></param>
        /// <returns></returns>
        public static BigInteger GetSellPrice(UInt160 pair)
        {
            Assert(BookExists(pair), "Book Not Exists");
            if (GetFirstOrderID(pair, true).Equals(0)) return 0;

            LimitOrder firstBuyOrder = GetFirstOrder(pair, true);
            return firstBuyOrder.price;
        }
    }
}
