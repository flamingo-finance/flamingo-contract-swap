using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;

namespace FlamingoSwapFactory
{
    public partial class FlamingoSwapFactoryContract
    {
        /// <summary>
        /// 断言
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                onFault(message);
                ExecutionEngine.Assert(false);
            }
        }

        /// <summary>
        /// 断言
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



        [OpCode(OpCode.APPEND)]
        private static extern void Append<T>(T[] array, T newItem);



        private static Iterator StorageFind(byte[] prefix)
        {
            return Storage.Find(Storage.CurrentContext, prefix, FindOptions.RemovePrefix);
        }



        private static ByteString StorageGet(ByteString key)
        {
            return Storage.Get(Storage.CurrentContext, key);
        }

        private static ByteString StorageGet(byte[] key)
        {
            return Storage.Get(Storage.CurrentContext, key);
        }

        private static void StoragePut(ByteString key, ByteString value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
        }

        private static void StoragePut(ByteString key, byte[] value)
        {
            Storage.Put(Storage.CurrentContext, key, (ByteString)value);
        }

        private static void StoragePut(byte[] key, byte[] value)
        {
            Storage.Put(Storage.CurrentContext, key, (ByteString)value);
        }

        private static void StoragePut(byte[] key, BigInteger value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
        }

        private static void StoragePut(byte[] key, ByteString value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
        }



        private static void StorageDelete(ByteString key)
        {
            Storage.Delete(Storage.CurrentContext, key);
        }

        private static void StorageDelete(byte[] key)
        {
            Storage.Delete(Storage.CurrentContext, (ByteString)key);
        }
    }
}
