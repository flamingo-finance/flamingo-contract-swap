using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace FlamingoSwapPair
{
    partial class FlamingoSwapPairContract
    {
        /// <summary>
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
                Runtime.Notify("Fault:" + message, data);
                throw new Exception(message);
            }
        }


        /// <summary>
        /// 断言Address为有效的地址格式
        /// </summary>
        /// <param name="input"></param>
        /// <param name="name"></param>
        private static void AssertAddress(byte[] input, string name)
        {
            Assert(input.Length == 20 && input.AsBigInteger() != 0, name + " is not address", input);
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
        private static bool DynamicTransfer(byte[] token, byte[] from, byte[] to, BigInteger amount)
        {
            var tokenACall = (Func<string, object[], bool>)token.ToDelegate();
            var args = new object[3];
            args[0] = from;
            args[1] = to;
            args[2] = amount;
            var result = tokenACall("transfer", args);
            return result;
        }



        /// <summary>
        /// 调用其它Nep5合约的“transfer”
        /// </summary>
        /// <param name="token"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private static bool SafeTransfer(byte[] token, byte[] from, byte[] to, BigInteger amount)
        {
            var tokenACall = (Func<string, object[], bool>)token.ToDelegate();
            var args = new object[3];
            args[0] = from;
            args[1] = to;
            args[2] = amount;
            var result = tokenACall("transfer", args);
            Assert(result, "Transfer Fail", token);
            return result;
        }

        /// <summary>
        /// 调用其它Nep5合约的“balanceOf”
        /// </summary>
        /// <param name="token"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        private static BigInteger DynamicBalanceOf(byte[] token, byte[] address)
        {
            var tokenACall = (Func<string, object[], BigInteger>)token.ToDelegate();
            var args = new object[1];
            args[0] = address;
            var result = tokenACall("balanceOf", args);
            return result;
        }
    }
}
