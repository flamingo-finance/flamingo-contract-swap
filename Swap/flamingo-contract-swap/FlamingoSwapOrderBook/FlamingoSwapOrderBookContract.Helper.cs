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
        /// <param name="pairKey"></param>
        /// <param name="order"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static bool InsertOrder(byte[] pairKey, uint id, LimitOrder order, bool isBuy)
        {
            uint firstID = GetFirstOrderID(pairKey, isBuy);

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
                SetFirstOrderID(pairKey, id, isBuy);
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
        /// <param name="pairKey"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static uint GetFirstOrderID(byte[] pairKey, bool isBuy)
        {
            Orderbook book = GetOrderbook(pairKey);
            return isBuy ? book.firstBuyID : book.firstSellID;
        }

        /// <summary>
        /// Set the first limit order id
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="id"></param>
        /// <param name="isBuy"></param>
        private static void SetFirstOrderID(byte[] pairKey, uint id, bool isBuy)
        {
            Orderbook book = GetOrderbook(pairKey);
            if (isBuy) book.firstBuyID = id;
            else book.firstSellID = id;
            SetOrderbook(pairKey, book);
        }

        /// <summary>
        /// Get the first limit order
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static LimitOrder GetFirstOrder(byte[] pairKey, bool isBuy)
        {
            uint id = GetFirstOrderID(pairKey, isBuy);
            return GetOrder(id);
        }

        /// <summary>
        /// Remove a canceled limit order from orderbook
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="id"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static bool RemoveOrder(byte[] pairKey, uint id, bool isBuy)
        {
            // Remove from BookMap
            Orderbook book = GetOrderbook(pairKey);
            uint firstID = isBuy ? book.firstBuyID : book.firstSellID;
            if (firstID == 0) return false;
            if (firstID == id)
            {
                // Delete the first
                SetFirstOrderID(pairKey, GetOrder(firstID).nextID, isBuy);
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
        /// <param name="pairKey"></param>
        /// <param name="isBuy"></param>
        private static void RemoveFirstOrder(byte[] pairKey, bool isBuy)
        {
            // Remove from BookMap
            Orderbook book = GetOrderbook(pairKey);
            uint firstID = isBuy ? book.firstBuyID : book.firstSellID;
            // Delete the first
            SetFirstOrderID(pairKey, GetOrder(firstID).nextID, isBuy);
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
        private static bool BookExists(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return bookMap.Get(pairKey) is not null;
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
        /// <param name="pairKey"></param>
        /// <returns></returns>
        private static Orderbook GetOrderbook(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return (Orderbook)StdLib.Deserialize(bookMap.Get(pairKey));
        }

        private static UInt160 GetBaseToken(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return ((Orderbook)StdLib.Deserialize(bookMap.Get(pairKey))).baseToken;
        }

        private static UInt160 GetQuoteToken(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return ((Orderbook)StdLib.Deserialize(bookMap.Get(pairKey))).quoteToken;
        }

        private static int GetQuoteDecimals(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return (int)((Orderbook)StdLib.Deserialize(bookMap.Get(pairKey))).quoteDecimals;
        }

        /// <summary>
        /// Update a book 
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="book"></param>
        private static void SetOrderbook(byte[] pairKey, Orderbook book)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            bookMap.Put(pairKey, StdLib.Serialize(book));
        }

        // private static void DeleteOrderBook(byte[] pairKey)
        // {
        //     StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
        //     bookMap.Delete(pairKey);
        // }

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
        /// Get unique pair key
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        private static byte[] GetPairKey(UInt160 tokenA, UInt160 tokenB)
        {
            return tokenA.ToUInteger() < tokenB.ToUInteger()
                ? BookMapKey.Concat(tokenA).Concat(tokenB)
                : BookMapKey.Concat(tokenB).Concat(tokenA);
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