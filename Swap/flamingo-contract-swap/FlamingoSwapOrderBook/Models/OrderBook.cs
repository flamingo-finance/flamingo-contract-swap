using Neo;
using Neo.SmartContract.Framework;
using System.Numerics;

namespace FlamingoSwapOrderBook
{
    public struct OrderBook
    {
        public UInt160 baseToken;
        public UInt160 quoteToken;
        public BigInteger quoteScale;
        public BigInteger minOrderAmount;
        public BigInteger maxOrderAmount;

        public ByteString firstBuyID;
        public ByteString firstSellID;
    }
}