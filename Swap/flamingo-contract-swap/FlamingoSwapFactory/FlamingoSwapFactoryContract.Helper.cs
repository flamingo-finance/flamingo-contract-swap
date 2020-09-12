using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace FlamingoSwapFactory
{
    partial class FlamingoSwapFactoryContract
    {
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


        [OpCode(OpCode.APPEND)]
        private static extern void Append<T>(T[] array, T newItem);
    }
}
