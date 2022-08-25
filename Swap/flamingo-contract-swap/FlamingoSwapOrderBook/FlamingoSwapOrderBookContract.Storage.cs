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
            var index = GetOrderIndex(id);
            return GetOrder(index, id);
        }

        [Safe]
        public static LimitOrder[] GetOrdersOnPage(UInt160 tokenA, UInt160 tokenB, BigInteger page)
        {
            var results = new LimitOrder[0];
            var orderMap = new StorageMap(Storage.CurrentReadOnlyContext, OrderMapPrefix);
            var prefix = GetBookInfo(tokenA, tokenB).Symbol + page;
            var iterator = orderMap.Find(prefix, FindOptions.ValuesOnly | FindOptions.DeserializeValues);
            while (iterator.Next()) Append(results, (LimitOrder)iterator.Value);
            return results;
        }

        [Safe]
        public static LimitOrder[] GetOrdersOf(UInt160 maker)
        {
            var results = new LimitOrder[0];
            var makerIndex = new StorageMap(Storage.CurrentReadOnlyContext, MakerIndexPrefix);
            var orderMap = new StorageMap(Storage.CurrentReadOnlyContext, OrderMapPrefix);
            var iterator = makerIndex.Find(maker, FindOptions.ValuesOnly);
            while (iterator.Next()) Append(results, (LimitOrder)StdLib.Deserialize(orderMap.Get((ByteString)iterator.Value)));
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

        private static ByteString AddLimitOrder(byte[] pairKey, string symbol, LimitOrder order)
        {
            // Get id and page
            var id = GetUnusedID();
            var page = GetFirstAvailablePage(pairKey);
            order.ID = id;
            order.Page = page;
            var index = symbol + order.Page;
            SetOrderIndex(id, index);
            SetMakerIndex(order.Maker + id, index + id);
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
            SetOrder(index, order.ID, order);
        }

        private static void RemoveLimitOrder(byte[] pairKey, ByteString index, LimitOrder order)
        {
            // Delete order and index 
            DeleteOrder(index, order.ID);
            DeleteMakerIndex(order.Maker + order.ID);
            DeleteOrderIndex(order.ID);

            // Update page status
            var pageOccupancy = GetPageOccupancy(pairKey, order.Page) - 1;
            Assert(pageOccupancy >= 0, "Invalid Page Occupancy");
            UpdatePageOccupancy(pairKey, order.Page, pageOccupancy);
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

        #region MakerIndex
        private static void SetMakerIndex(ByteString index, ByteString orderIndex)
        {
            var makerIndex = new StorageMap(Storage.CurrentContext, MakerIndexPrefix);
            makerIndex.Put(index, orderIndex);
        }

        private static ByteString GetMakerIndex(ByteString index)
        {
            var makerIndex = new StorageMap(Storage.CurrentReadOnlyContext, MakerIndexPrefix);
            return makerIndex.Get(index);
        }

        private static void DeleteMakerIndex(ByteString index)
        {
            var makerIndex = new StorageMap(Storage.CurrentContext, MakerIndexPrefix);
            makerIndex.Delete(index);
        }
        #endregion

        #region OrderIndex
        private static void SetOrderIndex(ByteString id, ByteString index)
        {
            var orderIndex = new StorageMap(Storage.CurrentContext, OrderIndexPrefix);
            orderIndex.Put(id, index);
        }

        private static ByteString GetOrderIndex(ByteString id)
        {
            var orderIndex = new StorageMap(Storage.CurrentReadOnlyContext, OrderIndexPrefix);
            return orderIndex.Get(id);
        }

        private static void DeleteOrderIndex(ByteString id)
        {
            var orderIndex = new StorageMap(Storage.CurrentContext, OrderIndexPrefix);
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
            counterMap.Put(pairKey, count);
        }

        private static BigInteger GetPageCounter(byte[] pairKey)
        {
            var counterMap = new StorageMap(Storage.CurrentReadOnlyContext, PageCounterKey);
            return (BigInteger)counterMap.Get(pairKey);
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