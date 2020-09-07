using System;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace FlamingoSwapRouter
{
    class FlamingoSwapRouterContract : SmartContract
    {
        public static bool Main()
        {
            Storage.Put("Hello", "World");
            return true;
        }
    }
}
