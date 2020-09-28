using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace FlamingoSwapPairWhiteList
{
    partial class FlamingoSwapPairWhiteList
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


        [OpCode(OpCode.APPEND)]
        private static extern void Append<T>(T[] array, T newItem);
    }
}
