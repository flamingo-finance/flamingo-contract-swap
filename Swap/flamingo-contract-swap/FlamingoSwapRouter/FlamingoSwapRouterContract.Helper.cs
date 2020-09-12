using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace FlamingoSwapRouter
{
    partial class FlamingoSwapRouterContract
    {


        /// <summary>
        /// 中断执行,节约gas
        /// </summary>
        /// <param name="message"></param>
        /// <param name="data"></param>
        private static void Throw(string message, params object[] data)
        {
            Runtime.Notify("Fault:" + message, data);
            throw new Exception(message);
        }



        /// <summary>
        /// 断言
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Runtime.Notify("Fault:" + message);
                throw new Exception(message);
            }
        }


        ///// <summary>
        ///// 断言,节约gas
        ///// </summary>
        ///// <param name="condition"></param>
        ///// <param name="message"></param>
        //[OpCode(OpCode.THROWIFNOT)]
        //[OpCode(OpCode.DROP)]
        //private static extern void Assert(bool condition, string message);





        /// <summary>
        /// 安全查询交易对，查不到立即中断合约执行
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        private static byte[] GetExchangePairWithAssert(byte[] tokenA, byte[] tokenB)
        {
            var pairContract = ((Func<string, object[], byte[]>)Factory.ToDelegate())("getExchangePair", new object[] { tokenA, tokenB });
            if (pairContract.Length != 20)
            {
                Throw("Cannot Find PairContract", tokenA, tokenB);
            }
            //Assert(pairContract.Length == 20, "cannot find pairContract");//+0.02 gas
            return pairContract;
        }


        /// <summary>
        /// 安全转账，失败则中断退出
        /// </summary>
        /// <param name="token"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        private static void SafeTransfer(byte[] token, byte[] from, byte[] to, BigInteger amount)
        {
            //var result = token.DynamicTransfer(from, to, amount);
            var result = ((Func<string, object[], bool>)token.ToDelegate())("transfer", new object[] { from, to, amount });
            if (!result)
            {
                Throw("Transfer Fail", token);
            }
            //Assert(result, "Transfer Fail", token);
        }
    }
}
