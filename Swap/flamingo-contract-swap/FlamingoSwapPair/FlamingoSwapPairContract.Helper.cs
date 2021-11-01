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
                throw new Exception(message);
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
                throw new Exception(message);
            }
        }


        /// <summary>
        /// 求平方根
        /// </summary>
        /// <param name="y"></param>
        /// <returns></returns>
        private static BigInteger Sqrt(BigInteger y)
        {
            if (y < 0) throw new InvalidOperationException("y can not be negative");
            if (y > 3)
            {
                var z = y;
                var x = y / 2 + 1;
                while (x < z)
                {
                    z = x;
                    x = (y / x + x) / 2;
                }

                return z;
            }
            else if (y != 0)
            {
                return 1;
            }
            return 0;
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
            var result = (bool)Contract.Call(token, "transfer", CallFlags.All, new object[] { from, to, amount, data });
            Assert(result, "Transfer Fail", token);
        }

        /// <summary>
        /// 调用其它Nep5合约的“balanceOf”
        /// </summary>
        /// <param name="token"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        private static BigInteger DynamicBalanceOf(UInt160 token, UInt160 address)
        {
            return (BigInteger)Contract.Call(token, "balanceOf", CallFlags.All, new object[] { address });
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
