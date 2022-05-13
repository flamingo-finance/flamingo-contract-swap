using System;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace FlamingoSwapRouter
{
    partial class FlamingoSwapRouterContract
    {
        //[Syscall("System.Runtime.Notify")]
        //private static extern void Notify(string eventName, params object[] data);

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
        /// 安全查询交易对，查不到立即中断合约执行
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
        /// 安全转账，失败则中断退出
        /// </summary>
        /// <param name="token"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        private static void SafeTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount)
        {
            try
            {
                var result = (bool)Contract.Call(token, "transfer", CallFlags.All, new object[] { from, to, amount, null });
                Assert(result, "Transfer Fail in Router", token);
            }
            catch (Exception)
            {
                Assert(false, "Transfer Error in Router", token);
            }
        }

        /// <summary>
        /// 请求转账，未授权则中断退出
        /// </summary>
        /// <param name="token"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        private static void RequestTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount)
        {
            try
            {
                var balanceBefore = (BigInteger)Contract.Call(token, "balanceOf", CallFlags.ReadOnly, new object[] { to });
                var result = (bool)Contract.Call(from, "approvedTransfer", CallFlags.All, new object[] { token, to, amount, null });
                var balanceAfter = (BigInteger)Contract.Call(token, "balanceOf", CallFlags.ReadOnly, new object[] { to });
                Assert(result, "Transfer Not Approved in Router", token);
                Assert(balanceAfter == balanceBefore + amount, "Unexpected Transfer in Router", token);
            }
            catch (Exception)
            {
                Assert(false, "Transfer Error in Router", token);
            }
        }


        private static ByteString StorageGet(string key)
        {
            return Storage.Get(Storage.CurrentContext, key);
        }

        private static void StoragePut(string key, string value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
        }

        private static void StoragePut(string key, byte[] value)
        {
            Storage.Put(Storage.CurrentContext, key, (ByteString)value);
        }

        private static void StoragePut(byte[] key, byte[] value)
        {
            Storage.Put(Storage.CurrentContext, key, (ByteString)value);
        }

        private static void StoragePut(string key, ByteString value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
        }
    }
}
