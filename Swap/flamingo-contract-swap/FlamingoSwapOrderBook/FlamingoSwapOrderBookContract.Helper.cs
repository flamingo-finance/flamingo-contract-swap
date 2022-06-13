using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
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
        /// <param name="id"></param>
        /// <param name="order"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static bool InsertOrder(byte[] pairKey, ByteString id, LimitOrder order, bool isBuy)
        {
            var firstID = GetFirstOrderID(pairKey, isBuy);

            // Check if there is no order
            var canBeFirst = firstID is null;
            if (!canBeFirst)
            {
                // Check if this order is the worthiest
                var firstOrder = GetOrder(firstID);
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

        private static bool InsertNotFirst(ByteString firstID, ByteString id, LimitOrder order, bool isBuy)
        {
            var currentID = firstID; 
            while (currentID is not null)
            {
                // Check before
                var currentOrder = GetOrder(currentID);
                var canFollow = (isBuy && (order.price <= currentOrder.price)) || (!isBuy && (order.price >= currentOrder.price));
                if (!canFollow) break;

                if (currentOrder.nextID is not null)
                {
                    // Check after
                    var nextOrder = GetOrder(currentOrder.nextID);
                    var canPrecede = (isBuy && (nextOrder.price < order.price)) || (!isBuy && (nextOrder.price > order.price));
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
        private static ByteString GetFirstOrderID(byte[] pairKey, bool isBuy)
        {
            var book = GetOrderBook(pairKey);
            return isBuy ? book.firstBuyID : book.firstSellID;
        }

        /// <summary>
        /// Set the first limit order id
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="id"></param>
        /// <param name="isBuy"></param>
        private static void SetFirstOrderID(byte[] pairKey, ByteString id, bool isBuy)
        {
            var book = GetOrderBook(pairKey);
            if (isBuy) book.firstBuyID = id;
            else book.firstSellID = id;
            SetOrderBook(pairKey, book);
        }

        /// <summary>
        /// Get the first limit order
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static LimitOrder GetFirstOrder(byte[] pairKey, bool isBuy)
        {
            var id = GetFirstOrderID(pairKey, isBuy);
            return GetOrder(id);
        }

        /// <summary>
        /// Remove a canceled limit order from orderbook
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="id"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static bool RemoveOrder(byte[] pairKey, ByteString id, bool isBuy)
        {
            // Remove from BookMap
            var book = GetOrderBook(pairKey);
            var firstID = isBuy ? book.firstBuyID : book.firstSellID;
            if (firstID is null) return false;
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

        private static bool RemoveNotFirst(ByteString firstID, ByteString id)
        {
            var currentID = firstID; 
            var currentOrder = GetOrder(currentID);
            while (currentOrder.nextID is not null)
            {
                // Check next
                if (currentOrder.nextID == id)
                {
                    // Do remove
                    var newNextID = GetOrder(currentOrder.nextID).nextID;
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
            var book = GetOrderBook(pairKey);
            var firstID = isBuy ? book.firstBuyID : book.firstSellID;
            // Delete the first
            SetFirstOrderID(pairKey, GetOrder(firstID).nextID, isBuy);
            DeleteOrder(firstID);
        }

        /// <summary>
        /// Check if a limit order exists
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private static bool OrderExists(ByteString id)
        {
            StorageMap orderMap = new(Storage.CurrentContext, OrderMapKey);
            return orderMap.Get(id) is not null;
        }

        /// <summary>
        /// Check if a order book exists
        /// </summary>
        /// <param name="pairKey"></param>
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
        private static LimitOrder GetOrder(ByteString id)
        {
            StorageMap orderMap = new(Storage.CurrentContext, OrderMapKey);
            return (LimitOrder)StdLib.Deserialize(orderMap.Get(id));
        }

        /// <summary>
        /// Update a limit order 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="order"></param>
        private static void SetOrder(ByteString id, LimitOrder order)
        {
            StorageMap orderMap = new(Storage.CurrentContext, OrderMapKey);
            orderMap.Put(id, StdLib.Serialize(order));
        }

        /// <summary>
        /// Delete a limit order 
        /// </summary>
        /// <param name="id"></param>
        private static void DeleteOrder(ByteString id)
        {
            StorageMap orderMap = new(Storage.CurrentContext, OrderMapKey);
            orderMap.Delete(id);
        }

        /// <summary>
        /// Get the detail of an order receipt
        /// </summary>
        /// <param name="maker"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private static OrderReceipt GetReceipt(UInt160 maker, ByteString id)
        {
            StorageMap receiptMap = new(Storage.CurrentContext, ReceiptMapKey);
            return (OrderReceipt)StdLib.Deserialize(receiptMap.Get(maker + id));
        }

        /// <summary>
        /// Get all receipts of the maker
        /// </summary>
        /// <param name="maker"></param>
        /// <returns></returns>
        private static OrderReceipt[] GetReceiptsOf(UInt160 maker)
        {
            StorageMap receiptMap = new(Storage.CurrentContext, ReceiptMapKey);
            var results = new OrderReceipt[0];
            var iterator = receiptMap.Find(maker, FindOptions.ValuesOnly | FindOptions.DeserializeValues);
            while (iterator.Next()) Append(results, (OrderReceipt)iterator.Value);
            return results;
        }

        [OpCode(OpCode.APPEND)]
        private static extern void Append<T>(T[] array, T newItem);

        /// <summary>
        /// Update an order receipt
        /// </summary>
        /// <param name="maker"></param>
        /// <param name="id"></param>
        /// <param name="receipt"></param>
        private static void SetReceipt(UInt160 maker, ByteString id, OrderReceipt receipt)
        {
            StorageMap receiptMap = new(Storage.CurrentContext, ReceiptMapKey);
            receiptMap.Put(maker + id, StdLib.Serialize(receipt));
        }

        /// <summary>
        /// Delete an order receipt
        /// </summary>
        /// <param name="maker"></param>
        /// <param name="id"></param>
        private static void DeleteReceipt(UInt160 maker, ByteString id)
        {
            StorageMap orderMap = new(Storage.CurrentContext, ReceiptMapKey);
            orderMap.Delete(maker + id);
        }

        /// <summary>
        /// Get the detail of a book 
        /// </summary>
        /// <param name="pairKey"></param>
        /// <returns></returns>
        private static OrderBook GetOrderBook(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return (OrderBook)StdLib.Deserialize(bookMap.Get(pairKey));
        }

        private static UInt160 GetBaseToken(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return ((OrderBook)StdLib.Deserialize(bookMap.Get(pairKey))).baseToken;
        }

        private static UInt160 GetQuoteToken(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return ((OrderBook)StdLib.Deserialize(bookMap.Get(pairKey))).quoteToken;
        }

        private static int GetQuoteDecimals(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return (int)((OrderBook)StdLib.Deserialize(bookMap.Get(pairKey))).quoteDecimals;
        }

        private static BigInteger GetMaxOrderAmount(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return (BigInteger)((OrderBook)StdLib.Deserialize(bookMap.Get(pairKey))).maxOrderAmount;
        }

        private static BigInteger GetMinOrderAmount(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            return (BigInteger)((OrderBook)StdLib.Deserialize(bookMap.Get(pairKey))).minOrderAmount;
        }

        /// <summary>
        /// Update a book 
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="book"></param>
        private static void SetOrderBook(byte[] pairKey, OrderBook book)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            bookMap.Put(pairKey, StdLib.Serialize(book));
        }

        private static void DeleteOrderBook(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapKey);
            bookMap.Delete(pairKey);
        }

        /// <summary>
        /// Find a random number as order ID 
        /// </summary>
        /// <returns></returns>
        private static ByteString GetUnusedID()
        {
            StorageContext context = Storage.CurrentContext;
            ByteString counter = Storage.Get(context, OrderIDKey);
            Storage.Put(context, OrderIDKey, (BigInteger)counter + 1);
            ByteString data = Runtime.ExecutingScriptHash;
            if (counter is not null) data += counter;
            return CryptoLib.Sha256(data);
        }

        /// <summary>
        /// Transfer NEP-5 tokens
        /// </summary>
        /// <param name="token"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private static void SafeTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount)
        {
            try
            {
                var result = (bool)Contract.Call(token, "transfer", CallFlags.All, new object[] { from, to, amount, null });
                Assert(result, "Transfer Fail in OrderBook", token);
            }
            catch (Exception)
            {
                Assert(false, "Transfer Error in OrderBook", token);
            }
        }

        private static void RequestTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount)
        {
            try
            {
                var balanceBefore = (BigInteger)Contract.Call(token, "balanceOf", CallFlags.ReadOnly, new object[] { to });
                var result = (bool)Contract.Call(from, "approvedTransfer", CallFlags.All, new object[] { token, to, amount, null });
                var balanceAfter = (BigInteger)Contract.Call(token, "balanceOf", CallFlags.ReadOnly, new object[] { to });
                Assert(result, "Transfer Not Approved in OrderBook", token);
                Assert(balanceAfter == balanceBefore + amount, "Unexpected Transfer in OrderBook", token);
            }
            catch (Exception)
            {
                Assert(false, "Transfer Error in OrderBook", token);
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

        private static ByteString StorageGet(ByteString key)
        {
            return Storage.Get(Storage.CurrentContext, key);
        }

        private static void StoragePut(ByteString key, ByteString value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
        }
    }
}