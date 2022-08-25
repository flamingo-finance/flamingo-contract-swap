using Neo;
using Neo.SmartContract.Framework;
using System.Numerics;

namespace FlamingoSwapOrderBook
{
    public struct LimitOrder
    {
        public UInt160 BaseToken;
        public UInt160 QuoteToken;
        public ByteString ID;
        public ulong Time;
        public bool IsBuy;
        public UInt160 Maker;
        public BigInteger Price;
        public BigInteger TotalAmount;
        public BigInteger LeftAmount;
        public BigInteger Page;
    }
}