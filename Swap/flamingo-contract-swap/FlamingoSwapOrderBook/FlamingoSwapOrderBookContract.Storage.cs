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
        private static BigInteger GetFirstAvailablePage()
        {
            var pageCount = GetPageCounter();
            for (var page = BigInteger.Zero; page < pageCount; page++)
            {
                if (GetPageOccupancy(page) < ORDER_PER_PAGE) return page;
            }
            return pageCount;
        }

        private static ByteString AddLimitOrder(LimitOrder order)
        {
            // Get id and page
            var id = GetUnusedID();
            var page = GetFirstAvailablePage();
            order.id = id;
            order.page = page;
            SetIndex(id, page);
            SetOrder(page, id, order);

            // Update page status
            if (page == GetPageCounter()) UpdatePageCounter(page + 1);
            var pageOccupancy = GetPageOccupancy(page) + 1;
            Assert(pageOccupancy <= ORDER_PER_PAGE, "Using Full Page");
            UpdatePageOccupancy(page, pageOccupancy);
            return id;
        }

        [Safe]
        public static LimitOrder GetLimitOrder(ByteString id)
        {
            var page = GetIndex(id);
            return GetOrder(page, id);
        }

        private static void RemoveLimitOrder(ByteString id)
        {
            // Delete order and index 
            var page = GetIndex(id);
            DeleteOrder(page, id);
            DeleteIndex(id);

            // Update page status
            var pageOccupancy = GetPageOccupancy(page) - 1;
            Assert(pageOccupancy >= 0, "Invalid Page Occupancy");
            UpdatePageOccupancy(page, pageOccupancy);
        }

        private static Iterator GetOrdersByPage(BigInteger page)
        {
            var orderMap = new StorageMap(Storage.CurrentContext, OrderMapPrefix);
            return orderMap.Find((ByteString)page, FindOptions.ValuesOnly | FindOptions.DeserializeValues);
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
            return (BookInfo)StdLib.Deserialize(bookMap.Get(pairKey));
        }
        #endregion

        #region OrderIndex
        private static void SetIndex(ByteString id, BigInteger page)
        {
            var orderIndex = new StorageMap(Storage.CurrentContext, OrderIndexKey);
            orderIndex.Put(id, page);
        }

        private static BigInteger GetIndex(ByteString id)
        {
            var orderIndex = new StorageMap(Storage.CurrentReadOnlyContext, OrderIndexKey);
            return (BigInteger)orderIndex.Get(id);
        }

        private static void DeleteIndex(ByteString id)
        {
            var orderIndex = new StorageMap(Storage.CurrentContext, OrderIndexKey);
            orderIndex.Delete(id);
        }
        #endregion

        #region OrderMap
        private static void SetOrder(BigInteger page, ByteString id, LimitOrder order)
        {
            var orderMap = new StorageMap(Storage.CurrentContext, OrderMapPrefix);
            orderMap.Put(page + id, StdLib.Serialize(order));
        }

        private static LimitOrder GetOrder(BigInteger page, ByteString id)
        {
            var orderMap = new StorageMap(Storage.CurrentReadOnlyContext, OrderMapPrefix);
            return (LimitOrder)StdLib.Deserialize(orderMap.Get(page + id));
        }

        private static void DeleteOrder(BigInteger page, ByteString id)
        {
            var orderMap = new StorageMap(Storage.CurrentContext, OrderMapPrefix);
            orderMap.Delete(page + id);
        }
        #endregion

        #region PageMap
        private static void UpdatePageOccupancy(BigInteger page, BigInteger amount)
        {
            var pageMap = new StorageMap(Storage.CurrentContext, PageMapPrefix);
            pageMap.Put((ByteString)page, amount.ToString());
        }

        [Safe]
        public static BigInteger GetPageOccupancy(BigInteger page)
        {
            var pageMap = new StorageMap(Storage.CurrentReadOnlyContext, PageMapPrefix);
            return (BigInteger)pageMap.Get((ByteString)page);
        }
        #endregion

        #region PageCounter
        private static void UpdatePageCounter(BigInteger count)
        {
            Storage.Put(Storage.CurrentContext, PageCounterKey, count);
        }

        [Safe]
        public static BigInteger GetPageCounter()
        {
            return (BigInteger)Storage.Get(Storage.CurrentReadOnlyContext, PageCounterKey);
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