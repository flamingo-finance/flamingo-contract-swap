using Neo;
using System.Numerics;

namespace FlamingoSwapOrderBook
{
    public struct BookInfo
    {
        public UInt160 baseToken;
        public UInt160 quoteToken;
        public BigInteger quoteScale;
        public BigInteger minOrderAmount;
        public BigInteger maxOrderAmount;
        public bool isPaused;
    }
}
