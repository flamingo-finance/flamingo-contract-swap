using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;

namespace FlamingoSwapOrderBook
{
    public static class Extensions
    {
        /// <summary>
        /// Is Valid and not Zero address
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static bool IsAddress(this UInt160 address)
        {
            return address.IsValid && !address.IsZero;
        }
    }
}
