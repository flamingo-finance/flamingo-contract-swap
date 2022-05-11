using System;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;

namespace FlamingoSwapPair
{
    public partial class FlamingoSwapPairContract
    {

        [Syscall("System.Runtime.Notify")]
        private static extern void Notify(string eventName, params object[] data);


        /// <summary>
        /// 断言
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                onFault(message, null);
                ExecutionEngine.Assert(false);
            }
        }

        /// <summary>
        /// 断言
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        /// <param name="data"></param>
        private static void Assert(bool condition, string message, params object[] data)
        {
            if (!condition)
            {
                onFault(message, data);
                ExecutionEngine.Assert(false);
            }
        }

        /// <summary>
        /// 调用其它Nep5合约的“transfer”
        /// </summary>
        /// <param name="token"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
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
        /// 调用其它Nep5合约的“balanceOf”
        /// </summary>
        /// <param name="token"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        private static BigInteger DynamicBalanceOf(UInt160 token, UInt160 address)
        {
            return (BigInteger)Contract.Call(token, "balanceOf", CallFlags.ReadOnly, new object[] { address });
        }




        private static ByteString StorageGet(ByteString key)
        {
            return Storage.Get(Storage.CurrentContext, key);
        }

        private static ByteString StorageGet(byte[] key)
        {
            return Storage.Get(Storage.CurrentContext, key);
        }
        //private static void StoragePut(string key, string value)
        //{
        //    Storage.Put(Storage.CurrentContext, key, value);
        //}

        //private static void StoragePut(string key, byte[] value)
        //{
        //    Storage.Put(Storage.CurrentContext, key, (ByteString)value);
        //}

        //private static void StoragePut(string key, BigInteger value)
        //{
        //    Storage.Put(Storage.CurrentContext, key, value);
        //}

        //private static void StoragePut(string key, ByteString value)
        //{
        //    Storage.Put(Storage.CurrentContext, key, value);
        //}   

        private static void StoragePut(ByteString key, ByteString value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
        }

        private static void StoragePut(ByteString key, byte[] value)
        {
            Storage.Put(Storage.CurrentContext, key, (ByteString)value);
        }

        private static void StoragePut(ByteString key, BigInteger value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
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
