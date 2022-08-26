using Neo;
using Neo.SmartContract.Framework;
using System.Numerics;

namespace FlamingoSwapOrderBook
{
    public struct LimitOrder
    {
        public UInt160 maker;
        public BigInteger price;
        public BigInteger amount;
        public ByteString nextID;
    }
}