using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.SmartContract.Framework.Services.Neo;

namespace FlamingoSwapRouter
{
    partial class FlamingoSwapRouterContract
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
    }
}
