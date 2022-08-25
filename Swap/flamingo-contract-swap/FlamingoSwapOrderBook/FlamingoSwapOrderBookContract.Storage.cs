using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using System.Numerics;

namespace FlamingoSwapOrderBook
{
    public partial class FlamingoSwapOrderBookContract
    {
        [Safe]
        public static BookInfo GetBookInfo(UInt160 tokenA, UInt160 tokenB)
        {
            return GetBook(GetPairKey(tokenA, tokenB));
        }

        [Safe]
        public static LimitOrder GetLimitOrder(ByteString id)
        {
            var index = GetIndex(id);
            return GetOrder(index, id);
        }

        [Safe]
        public static LimitOrder[] GetOrdersOnPage(UInt160 tokenA, UInt160 tokenB, BigInteger page)
        {
            var results = new LimitOrder[0];
            var orderMap = new StorageMap(Storage.CurrentReadOnlyContext, OrderMapPrefix);
            var iterator = orderMap.Find(GetPairKey(tokenA, tokenB).ToByteString() + page, FindOptions.ValuesOnly | FindOptions.DeserializeValues);
            while (iterator.Next()) Append(results, (LimitOrder)iterator.Value);
            return results;
        }

        [Safe]
        public static LimitOrder[] GetOrdersOf(UInt160 maker)
        {
            var results = new LimitOrder[0];
            var orderMap = new StorageMap(Storage.CurrentReadOnlyContext, OrderMapPrefix);
            var iterator = orderMap.Find(maker, FindOptions.ValuesOnly | FindOptions.DeserializeValues);
            while (iterator.Next()) Append(results, (LimitOrder)iterator.Value);
            return results;
        }

        private static BigInteger GetFirstAvailablePage(byte[] pairKey)
        {
            var pageCount = GetPageCounter(pairKey);
            for (var page = BigInteger.One; page <= pageCount; page++)
            {
                if (GetPageOccupancy(pairKey, page) < ORDER_PER_PAGE) return page;
            }
            return pageCount + 1;
        }

        private static ByteString AddLimitOrder(byte[] pairKey, LimitOrder order)
        {
            // Get id and page
            var id = GetUnusedID();
            var page = GetFirstAvailablePage(pairKey);
            order.id = id;
            order.page = page;
            var index = pairKey.ToByteString() + order.page + order.maker;
            SetIndex(id, index);
            SetOrder(index, id, order);

            // Update page status
            if (page > GetPageCounter(pairKey)) UpdatePageCounter(pairKey, page);
            var pageOccupancy = GetPageOccupancy(pairKey, page) + 1;
            Assert(pageOccupancy <= ORDER_PER_PAGE, "Using Full Page");
            UpdatePageOccupancy(pairKey, page, pageOccupancy);
            return id;
        }

        private static void UpdateLimitOrder(ByteString index, LimitOrder order)
        {
            SetOrder(index, order.id, order);
        }

        private static void RemoveLimitOrder(byte[] pairKey, ByteString index, LimitOrder order)
        {
            // Delete order and index 
            DeleteOrder(index, order.id);
            DeleteIndex(order.id);

            // Update page status
            var pageOccupancy = GetPageOccupancy(pairKey, order.page) - 1;
            Assert(pageOccupancy >= 0, "Invalid Page Occupancy");
            UpdatePageOccupancy(pairKey, order.page, pageOccupancy);
        }

        #region BookMap
        private static void SetBook(byte[] pairKey, BookInfo book)
        {
            var bookMap = new StorageMap(Storage.CurrentContext, BookMapPrefix);
            bookMap.Put(pairKey, StdLib.Serialize(book));
        }

        private static BookInfo GetBook(byte[] pairKey)
        {
            var bookMap = new StorageMap(Storage.CurrentReadOnlyContext, BookMapPrefix);
            var bookInfo = bookMap.Get(pairKey);
            return bookInfo is null ? new BookInfo() : (BookInfo)StdLib.Deserialize(bookInfo);
        }
        #endregion

        #region OrderIndex
        private static void SetIndex(ByteString id, ByteString index)
        {
            var orderIndex = new StorageMap(Storage.CurrentContext, OrderIndexKey);
            orderIndex.Put(id, index);
        }

        private static ByteString GetIndex(ByteString id)
        {
            var orderIndex = new StorageMap(Storage.CurrentReadOnlyContext, OrderIndexKey);
            return orderIndex.Get(id);
        }

        private static void DeleteIndex(ByteString id)
        {
            var orderIndex = new StorageMap(Storage.CurrentContext, OrderIndexKey);
            orderIndex.Delete(id);
        }
        #endregion

        #region OrderMap
        private static void SetOrder(ByteString index, ByteString id, LimitOrder order)
        {
            var orderMap = new StorageMap(Storage.CurrentContext, OrderMapPrefix);
            orderMap.Put(index + id, StdLib.Serialize(order));
        }

        private static LimitOrder GetOrder(ByteString index, ByteString id)
        {
            var orderMap = new StorageMap(Storage.CurrentReadOnlyContext, OrderMapPrefix);
            var order = orderMap.Get(index + id);
            return order is null ? new LimitOrder() : (LimitOrder)StdLib.Deserialize(order);
        }

        private static void DeleteOrder(ByteString index, ByteString id)
        {
            var orderMap = new StorageMap(Storage.CurrentContext, OrderMapPrefix);
            orderMap.Delete(index + id);
        }
        #endregion

        #region PageMap
        private static void UpdatePageOccupancy(byte[] pairKey, BigInteger page, BigInteger amount)
        {
            var pageMap = new StorageMap(Storage.CurrentContext, PageMapPrefix);
            pageMap.Put(pairKey.ToByteString() + page, amount);
        }

        private static BigInteger GetPageOccupancy(byte[] pairKey, BigInteger page)
        {
            var pageMap = new StorageMap(Storage.CurrentReadOnlyContext, PageMapPrefix);
            return (BigInteger)pageMap.Get(pairKey.ToByteString() + page);
        }
        #endregion

        #region PageCounter
        private static void UpdatePageCounter(byte[] pairKey, BigInteger count)
        {
            var counterMap = new StorageMap(Storage.CurrentContext, PageCounterKey);
            counterMap.Put(pairKey, count - 1);
        }

        private static BigInteger GetPageCounter(byte[] pairKey)
        {
            var counterMap = new StorageMap(Storage.CurrentReadOnlyContext, PageCounterKey);
            return (BigInteger)counterMap.Get(pairKey) + 1;
        }
        #endregion

        #region OrderCounter
        /// <summary>
        /// Find a random number as order ID 
        /// </summary>
        /// <returns></returns>
        private static ByteString GetUnusedID()
        {
            var context = Storage.CurrentContext;
            var counter = Storage.Get(context, OrderCounterKey);
            Storage.Put(context, OrderCounterKey, (BigInteger)counter + 1);
            var data = (ByteString)Runtime.ExecutingScriptHash;
            if (counter is not null) data += counter;
            return CryptoLib.Sha256(data);
        }
        #endregion

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
    }
}