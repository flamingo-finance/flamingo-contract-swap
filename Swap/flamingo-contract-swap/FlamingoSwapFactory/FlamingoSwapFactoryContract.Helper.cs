using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace FlamingoSwapFactory
{
    partial class FlamingoSwapFactoryContract
    {

        /// <summary>
        /// send notify
        /// </summary>
        /// <param name="message"></param>
        private static void Notify(string message)
        {
            Notify(message, new object[0]);
        }

        [Syscall("System.Runtime.Notify")]
        private static extern void Notify(string eventName, params object[] data);


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
                Notify("Fault:" + message, data);
                throw new Exception(message);
            }
        }


        ///// <summary>
        ///// 断言Address为有效的地址格式
        ///// </summary>
        ///// <param name="input"></param>
        ///// <param name="name"></param>
        //private static void AssertAddress(byte[] input, string name)
        //{
        //    Assert(input.Length == 20 && input.AsBigInteger() != 0, name + " is not address", input);
        //}


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
