using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using System;
using System.Numerics;

namespace FlamingoSwapOrderBook
{
    public partial class FlamingoSwapOrderBookContract
    {
        /// <summary>
        /// Insert a not-fully-deal limit order into orderbook
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="order"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static bool InsertOrder(UInt160 pair, uint id, LimitOrder order, bool isBuy)
        {
            uint firstID = GetFirstOrderID(pair, isBuy);

            // Check if there is no order
            bool canBeFirst = firstID == 0;
            if (!canBeFirst)
            {
                // Check if this order is the worthiest
                LimitOrder firstOrder = GetOrder(firstID);
                canBeFirst = (isBuy && (firstOrder.price < order.price)) || (!isBuy && (firstOrder.price > order.price));
            }
            if (canBeFirst)
            {
                // Insert to the first
                SetFirstOrderID(pair, id, isBuy);
                order.nextID = firstID;
                SetOrder(id, order);
                return true;
            }
            else
            {
                // Find the position
                return InsertNotFirst(firstID, id, order, isBuy);
            }
        }

        private static bool InsertNotFirst(uint firstID, uint id, LimitOrder order, bool isBuy)
        {
            uint currentID = firstID; 
            LimitOrder currentOrder;
            LimitOrder nextOrder;
            while (currentID != 0)
            {
                // Check before
                currentOrder = GetOrder(currentID);
                bool canFollow = (isBuy && (order.price <= currentOrder.price)) || (!isBuy && (order.price >= currentOrder.price));
                if (!canFollow) break;

                if (currentOrder.nextID != 0)
                {
                    // Check after
                    nextOrder = GetOrder(currentOrder.nextID);
                    bool canPrecede = (isBuy && (nextOrder.price < order.price)) || (!isBuy && (nextOrder.price > order.price));
                    canFollow = canFollow && canPrecede;
                }
                if (canFollow)
                {
                    // Do insert
                    order.nextID = currentOrder.nextID;
                    SetOrder(id, order);
                    currentOrder.nextID = id;
                    SetOrder(currentID, currentOrder);
                    return true;
                }
                currentID = currentOrder.nextID;
            }
            return false;
        }

        /// <summary>
        /// Get the first limit order id
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static uint GetFirstOrderID(UInt160 pair, bool isBuy)
        {
            Orderbook book = GetOrderbook(pair);
            return isBuy ? book.firstBuyID : book.firstSellID;
        }

        /// <summary>
        /// Set the first limit order id
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="id"></param>
        /// <param name="isBuy"></param>
        private static void SetFirstOrderID(UInt160 pair, uint id, bool isBuy)
        {
            Orderbook book = GetOrderbook(pair);
            if (isBuy) book.firstBuyID = id;
            else book.firstSellID = id;
            SetOrderbook(pair, book);
        }

        /// <summary>
        /// Get the first limit order
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static LimitOrder GetFirstOrder(UInt160 pair, bool isBuy)
        {
            uint id = GetFirstOrderID(pair, isBuy);
            return GetOrder(id);
        }

        /// <summary>
        /// Remove a canceled limit order from orderbook
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="id"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static bool RemoveOrder(UInt160 pair, uint id, bool isBuy)
        {
            // Remove from BookMap
            Orderbook book = GetOrderbook(pair);
            uint firstID = isBuy ? book.firstBuyID : book.firstSellID;
            if (firstID == 0) return false;
            if (firstID == id)
            {
                // Delete the first
                SetFirstOrderID(pair, GetOrder(firstID).nextID, isBuy);
                DeleteOrder(firstID);
                return true;
            }
            else
            {
                // Find the position
                return RemoveNotFirst(firstID, id);
            }
        }

        private static bool RemoveNotFirst(uint firstID, uint id)
        {
            uint currentID = firstID; 
            LimitOrder currentOrder = GetOrder(currentID);
            while (currentOrder.nextID != 0)
            {
                // Check next
                if (currentOrder.nextID == id)
                {
                    // Do remove
                    uint newNextID = GetOrder(currentOrder.nextID).nextID;
                    DeleteOrder(currentOrder.nextID);
                    currentOrder.nextID = newNextID;
                    SetOrder(currentID, currentOrder);
                    return true;
                }
                currentID = currentOrder.nextID;
                currentOrder = GetOrder(currentID);
            }
            return false;
        }

        /// <summary>
        /// Remove a fully-deal limit order from orderbook
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="isBuy"></param>
        private static void RemoveFirstOrder(UInt160 pair, bool isBuy)
        {
            // Remove from BookMap
            Orderbook book = GetOrderbook(pair);
            uint firstID = isBuy ? book.firstBuyID : book.firstSellID;
            // Delete the first
            SetFirstOrderID(pair, GetOrder(firstID).nextID, isBuy);
            DeleteOrder(firstID);
        }

        /// <summary>
        /// Check if a limit order exists
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private static bool OrderExists(uint id)
        {
            StorageMap orderMap = new(Storage.CurrentContext, OrderMapKey);
            return orderMap.Get(id.ToString()) is not null;
        }

        /// <summary>
        /// Check if a order book exists
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private static bool BookExists(UInt160 pair)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return bookMap.Get(pair) is not null;
        }

        /// <summary>
        /// Get the detail of a limit order 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private static LimitOrder GetOrder(uint id)
        {
            StorageMap orderMap = new(Storage.CurrentContext, OrderMapKey);
            return (LimitOrder)StdLib.Deserialize(orderMap.Get(id.ToString()));
        }

        /// <summary>
        /// Update a limit order 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="order"></param>
        private static void SetOrder(uint id, LimitOrder order)
        {
            StorageMap orderMap = new(Storage.CurrentContext, OrderMapKey);
            orderMap.Put(id.ToString(), StdLib.Serialize(order));
        }

        /// <summary>
        /// Delete a limit order 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="order"></param>
        private static void DeleteOrder(uint id)
        {
            StorageMap orderMap = new(Storage.CurrentContext, OrderMapKey);
            orderMap.Delete(id.ToString());
        }

        /// <summary>
        /// Get the detail of a book 
        /// </summary>
        /// <param name="pair"></param>
        /// <returns></returns>
        private static Orderbook GetOrderbook(UInt160 pair)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return (Orderbook)StdLib.Deserialize(bookMap.Get(pair));
        }

        private static UInt160 GetBaseToken(UInt160 pair)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return ((Orderbook)StdLib.Deserialize(bookMap.Get(pair))).baseToken;
        }

        private static UInt160 GetQuoteToken(UInt160 pair)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return ((Orderbook)StdLib.Deserialize(bookMap.Get(pair))).quoteToken;
        }

        /// <summary>
        /// Update a book 
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="book"></param>
        private static void SetOrderbook(UInt160 pair, Orderbook book)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            bookMap.Put(pair, StdLib.Serialize(book));
        }

        /// <summary>
        /// Find a random number as order ID 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private static uint GetUnusedID(uint id = 0)
        {
            if (id == 0)
            {
                id = (uint)(Runtime.GetRandom() % MAX_ID);
            }
            // Find available
            for (int i = 0; i < 100 && id <= MAX_ID; i++)
            {
                if (!OrderExists(id)) return id;
                id++;
            }
            Assert(false, "No Available Order ID");
            return 0;
        }

        /// <summary>
        /// Buy below the expected price
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="buyer"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private static BigInteger DealBuy(UInt160 pair, UInt160 buyer, BigInteger price, BigInteger amount)
        {
            Orderbook book = GetOrderbook(pair);
            UInt160 me = Runtime.ExecutingScriptHash;
            while (amount > 0 && GetFirstOrderID(pair, false) != 0)
            {
                // Check the lowest sell price
                if (GetMarketPrice(pair, false) > price) break;

                LimitOrder firstOrder = GetFirstOrder(pair, false);
                if (firstOrder.amount <= amount)
                {
                    // Full-fill
                    amount -= firstOrder.amount;
                    // Do transfer
                    SafeTransfer(book.quoteToken, me, firstOrder.sender, firstOrder.amount * firstOrder.price);
                    SafeTransfer(book.baseToken, me, buyer, firstOrder.amount);
                    onDealOrder(pair, GetFirstOrderID(pair, false), price, firstOrder.amount, 0);
                    // Remove full-fill order
                    RemoveFirstOrder(pair, false);
                }
                else
                {
                    // Part-fill
                    firstOrder.amount -= amount;
                    // Do transfer
                    SafeTransfer(book.quoteToken, me, firstOrder.sender, amount * firstOrder.price);
                    SafeTransfer(book.baseToken, me, buyer, amount);
                    onDealOrder(pair, GetFirstOrderID(pair, false), price, amount, firstOrder.amount);
                    // Update order
                    SetOrder(GetFirstOrderID(pair, false), firstOrder);
                    amount = 0;
                }
            }
            return amount;
        }

        /// <summary>
        /// Sell above the expected price
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="seller"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private static BigInteger DealSell(UInt160 pair, UInt160 seller, BigInteger price, BigInteger amount)
        {
            Orderbook book = GetOrderbook(pair);
            UInt160 me = Runtime.ExecutingScriptHash;
            while (amount > 0 && GetFirstOrderID(pair, true) != 0)
            {
                // Check the highest buy price
                if (GetMarketPrice(pair, true) < price) break;

                LimitOrder firstOrder = GetFirstOrder(pair, true);
                if (firstOrder.amount <= amount)
                {
                    // Full-fill
                    amount -= firstOrder.amount;
                    // Do transfer
                    SafeTransfer(book.baseToken, me, firstOrder.sender, firstOrder.amount);
                    SafeTransfer(book.quoteToken, me, seller, firstOrder.amount * firstOrder.price);
                    onDealOrder(pair, GetFirstOrderID(pair, true), firstOrder.price, firstOrder.amount, 0);
                    // Remove full-fill order
                    RemoveFirstOrder(pair, true);
                }
                else
                {
                    // Part-fill
                    firstOrder.amount -= amount;
                    // Do transfer
                    SafeTransfer(book.baseToken, me, firstOrder.sender, amount);
                    SafeTransfer(book.quoteToken, me, seller, amount * firstOrder.price);
                    onDealOrder(pair, GetFirstOrderID(pair, true), firstOrder.price, amount, firstOrder.amount);
                    // Update order
                    SetOrder(GetFirstOrderID(pair, true), firstOrder);
                    amount = 0;
                }
            }
            return amount;
        }

        /// <summary>
        /// Internal price reporter
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static BigInteger GetMarketPrice(UInt160 pair, bool isBuy)
        {
            LimitOrder firstOrder = GetFirstOrder(pair, isBuy);
            return firstOrder.price;
        }

        /// <summary>
        /// Handle NEP-5 transaction
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="token"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private static void SafeTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount, byte[] data = null)
        {
            try
            {
                var result = (bool)Contract.Call(token, "transfer", CallFlags.All, new object[] { from, to, amount, data });
                Assert(result, "Transfer Fail in Pair", token);
            }
            catch (Exception)
            {
                Assert(false, "Catch Transfer Error in Pair", token);
            }
        }

        /// <summary>
        /// Check if
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        /// <param name="data"></param>
        private static void Assert(bool condition, string message, object data = null)
        {
            if (!condition)
            {
                onFault(message, data);
                ExecutionEngine.Assert(false);
            }
        }
    }
}