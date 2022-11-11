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
        /// <param name="book"></param>
        /// <param name="id"></param>
        /// <param name="order"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static bool InsertOrder(byte[] pairKey, OrderBook book, ByteString id, LimitOrder order, bool isBuy)
        {
            var firstID = isBuy ? book.firstBuyID : book.firstSellID;

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
                if (isBuy) book.firstBuyID = id;
                else book.firstSellID = id;
                SetOrderBook(pairKey, book);
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

        private static bool InsertOrderAt(ByteString parentID, ByteString id, LimitOrder order, bool isBuy)
        {
            var parentOrder = GetOrder(parentID);
            var canFollow = (isBuy && (order.price <= parentOrder.price)) || (!isBuy && (order.price >= parentOrder.price));
            if (!canFollow) return false;
            
            if (parentOrder.nextID is not null)
            {
                // Check after
                var nextOrder = GetOrder(parentOrder.nextID);
                var canPrecede = (isBuy && (nextOrder.price < order.price)) || (!isBuy && (nextOrder.price > order.price));
                canFollow = canFollow && canPrecede;
            }
            if (canFollow)
            {
                // Do insert
                order.nextID = parentOrder.nextID;
                SetOrder(id, order);
                parentOrder.nextID = id;
                SetOrder(parentID, parentOrder);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get the parent order id
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="isBuy"></param>
        /// <param name="childID"></param>
        /// <returns></returns>
        private static ByteString GetParentID(byte[] pairKey, bool isBuy, ByteString childID)
        {
            var book = GetOrderBook(pairKey);
            var firstID = isBuy ? book.firstBuyID : book.firstSellID;
            if (firstID == childID) return null;

            var currentID = firstID;
            while (currentID is not null)
            {
                var currentOrder = GetOrder(currentID);
                if (currentOrder.nextID == childID) return currentID;
                currentID = currentOrder.nextID;
            }
            return null;
        }

        /// <summary>
        /// Remove a canceled limit order from orderbook
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="book"></param>
        /// <param name="id"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        private static bool RemoveOrder(byte[] pairKey, OrderBook book, ByteString id, bool isBuy)
        {
            // Remove from BookMap
            var firstID = isBuy ? book.firstBuyID : book.firstSellID;
            if (firstID is null) return false;
            if (firstID == id)
            {
                // Delete the first
                if (isBuy) book.firstBuyID = GetOrder(firstID).nextID;
                else book.firstSellID = GetOrder(firstID).nextID;
                SetOrderBook(pairKey, book);
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

        private static bool RemoveOrderAt(ByteString parentID, ByteString id)
        {
            var parentOrder = GetOrder(parentID);
            if (parentOrder.nextID != id) return false;

            // Do remove
            var newNextID = GetOrder(id).nextID;
            DeleteOrder(id);
            parentOrder.nextID = newNextID;
            SetOrder(parentID, parentOrder);
            return true;
        }

        /// <summary>
        /// Check if a limit order exists
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private static bool OrderExists(ByteString id)
        {
            StorageMap orderMap = new(Storage.CurrentReadOnlyContext, OrderMapPrefix);
            return orderMap.Get(id) is not null;
        }

        /// <summary>
        /// Check if an order book exists
        /// </summary>
        /// <param name="pairKey"></param>
        /// <returns></returns>
        private static bool BookExists(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentReadOnlyContext, BookMapPrefix);
            return bookMap.Get(pairKey) is not null;
        }

        /// <summary>
        /// Set an order book as paused
        /// </summary>
        /// <param name="pairKey"></param>
        /// <returns></returns>
        private static void SetPaused(byte[] pairKey)
        {
            StorageMap pauseMap = new(Storage.CurrentContext, PauseMapPrefix);
            pauseMap.Put(pairKey, 1);
        }

        /// <summary>
        /// Remove an order book from paused
        /// </summary>
        /// <param name="pairKey"></param>
        /// <returns></returns>
        private static void RemovePaused(byte[] pairKey)
        {
            StorageMap pauseMap = new(Storage.CurrentContext, PauseMapPrefix);
            pauseMap.Delete(pairKey);
        }

        /// <summary>
        /// Check if an order book is paused
        /// </summary>
        /// <param name="pairKey"></param>
        /// <returns></returns>
        private static bool BookPaused(byte[] pairKey)
        {
            StorageMap pauseMap = new(Storage.CurrentReadOnlyContext, PauseMapPrefix);
            return pauseMap.Get(pairKey) is not null;
        }

        /// <summary>
        /// Get the detail of a limit order 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private static LimitOrder GetOrder(ByteString id)
        {
            StorageMap orderMap = new(Storage.CurrentReadOnlyContext, OrderMapPrefix);
            var order = orderMap.Get(id);
            return order is null ? new LimitOrder() : (LimitOrder)StdLib.Deserialize(order);
        }

        /// <summary>
        /// Update a limit order 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="order"></param>
        private static void SetOrder(ByteString id, LimitOrder order)
        {
            StorageMap orderMap = new(Storage.CurrentContext, OrderMapPrefix);
            orderMap.Put(id, StdLib.Serialize(order));
        }

        /// <summary>
        /// Delete a limit order 
        /// </summary>
        /// <param name="id"></param>
        private static void DeleteOrder(ByteString id)
        {
            StorageMap orderMap = new(Storage.CurrentContext, OrderMapPrefix);
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
            StorageMap receiptMap = new(Storage.CurrentReadOnlyContext, ReceiptMapPrefix);
            var receipt = receiptMap.Get(maker + id);
            return receipt is null ? new OrderReceipt() : (OrderReceipt)StdLib.Deserialize(receipt);
        }

        /// <summary>
        /// Get all receipts of the maker
        /// </summary>
        /// <param name="maker"></param>
        /// <returns></returns>
        private static Iterator ReceiptsOf(UInt160 maker)
        {
            StorageMap receiptMap = new(Storage.CurrentReadOnlyContext, ReceiptMapPrefix);
            return receiptMap.Find(maker, FindOptions.ValuesOnly | FindOptions.DeserializeValues);
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
            StorageMap receiptMap = new(Storage.CurrentContext, ReceiptMapPrefix);
            receiptMap.Put(maker + id, StdLib.Serialize(receipt));
        }

        /// <summary>
        /// Delete an order receipt
        /// </summary>
        /// <param name="maker"></param>
        /// <param name="id"></param>
        private static void DeleteReceipt(UInt160 maker, ByteString id)
        {
            StorageMap receiptMap = new(Storage.CurrentContext, ReceiptMapPrefix);
            receiptMap.Delete(maker + id);
        }

        /// <summary>
        /// Get the detail of a book 
        /// </summary>
        /// <param name="pairKey"></param>
        /// <returns></returns>
        private static OrderBook GetOrderBook(byte[] pairKey)
        {
            StorageMap bookMap = new(Storage.CurrentReadOnlyContext, BookMapPrefix);
            var book = bookMap.Get(pairKey);
            return book is null ? new OrderBook() : (OrderBook)StdLib.Deserialize(book);
        }

        /// <summary>
        /// Update a book 
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="book"></param>
        private static void SetOrderBook(byte[] pairKey, OrderBook book)
        {
            StorageMap bookMap = new(Storage.CurrentContext, BookMapPrefix);
            bookMap.Put(pairKey, StdLib.Serialize(book));
        }

        /// <summary>
        /// Stage the fundfee payment for later claim
        /// </summary>
        /// <param name="token"></param>
        /// <param name="amount"></param>
        private static void StageFundFee(UInt160 token, BigInteger amount)
        {
            Assert(amount >= 0, "Invalid Fee Amount");
            StorageMap feeMap = new(Storage.CurrentContext, FeeMapPrefix);
            feeMap.Put(token, (BigInteger)feeMap.Get(token) + amount);
        }

        /// <summary>
        /// Get staged fundfee amount
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private static BigInteger GetStagedFundFee(UInt160 token)
        {
            StorageMap feeMap = new(Storage.CurrentReadOnlyContext, FeeMapPrefix);
            return (BigInteger)feeMap.Get(token);
        }

        /// <summary>
        /// Reset the staged fundfee amount to 0
        /// </summary>
        /// <param name="token"></param>
        private static void CleanStagedFundFee(UInt160 token)
        {
            StorageMap feeMap = new(Storage.CurrentContext, FeeMapPrefix);
            feeMap.Delete(token);
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
        /// Get the pair contract
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        public static UInt160 GetExchangePairWithAssert(UInt160 tokenA, UInt160 tokenB)
        {
            Assert(tokenA.IsValid && tokenB.IsValid, "Invalid A or B Address");
            var pairContract = (byte[])Contract.Call(Factory, "getExchangePair", CallFlags.ReadOnly, new object[] { tokenA, tokenB });
            Assert(pairContract != null && pairContract.Length == 20, "PairContract Not Found", tokenA, tokenB);
            return (UInt160)pairContract;
        }

        /// <summary>
        /// Get TokenA and TokenB reserves from paicontract
        /// </summary>
        /// <param name="pairContract"></param>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        public static BigInteger[] GetReserves(UInt160 pairContract, UInt160 tokenA, UInt160 tokenB)
        {
            var reserveData = (ReservesData)Contract.Call(pairContract, "getReserves", CallFlags.ReadOnly, new object[] { });
            return tokenA.ToUInteger() < tokenB.ToUInteger() ? new BigInteger[] { reserveData.Reserve0, reserveData.Reserve1 } : new BigInteger[] { reserveData.Reserve1, reserveData.Reserve0 };
        }

        /// <summary>
        /// Check if pair contract charge a fundfee
        /// </summary>
        /// <param name="pairContract"></param>
        /// <returns></returns>
        public static bool HasFundAddress(UInt160 pairContract)
        {
            return (byte[])Contract.Call(pairContract, "getFundAddress", CallFlags.ReadOnly, new object[] { }) != null;
        }

        /// <summary>
        /// Get amountOut from pair
        /// </summary>
        /// <param name="pairContract"></param>
        /// <param name="amount0Out"></param>
        /// <param name="amount1Out"></param>
        /// <param name="toAddress"></param>
        private static void SwapOut(UInt160 pairContract, BigInteger amount0Out, BigInteger amount1Out, UInt160 toAddress)
        {
            Contract.Call(pairContract, "swap", CallFlags.All, new object[] { amount0Out, amount1Out, toAddress, null });
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
        private static void SafeTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount, byte[] data = null)
        {
            try
            {
                var result = (bool)Contract.Call(token, "transfer", CallFlags.All, new object[] { from, to, amount, data });
                Assert(result, "Transfer Fail in OrderBook", token);
            }
            catch (Exception)
            {
                Assert(false, "Transfer Error in OrderBook", token);
            }
        }

        private static void RequestTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount, byte[] data = null)
        {
            try
            {
                var balanceBefore = GetBalanceOf(token, to);
                var result = (bool)Contract.Call(from, "approvedTransfer", CallFlags.All, new object[] { token, to, amount, data });
                var balanceAfter = GetBalanceOf(token, to);
                Assert(result, "Transfer Not Approved in OrderBook", token);
                Assert(balanceAfter == balanceBefore + amount, "Unexpected Transfer in OrderBook", token);
            }
            catch (Exception)
            {
                Assert(false, "Transfer Error in OrderBook", token);
            }
        }

        private static BigInteger GetBalanceOf(UInt160 token, UInt160 address)
        {
            return (BigInteger)Contract.Call(token, "balanceOf", CallFlags.ReadOnly, new object[] { address });
        }

        /// <summary>
        /// Get unique pair key
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        private static byte[] GetPairKey(UInt160 tokenA, UInt160 tokenB)
        {
            return (BigInteger)tokenA < (BigInteger)tokenB
                ? BookMapPrefix.Concat(tokenA).Concat(tokenB)
                : BookMapPrefix.Concat(tokenB).Concat(tokenA);
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
                throw new Exception(message);
            }
        }

        /// <summary>
        /// Check if
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        /// <param name="data"></param>
        private static void Assert(bool condition, string message, params object[] data)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }
    }
}
