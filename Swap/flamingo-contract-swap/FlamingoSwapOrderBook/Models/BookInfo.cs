using Neo;
using System.Numerics;

namespace FlamingoSwapOrderBook
{
    public struct BookInfo
    {
        public string Symbol;
        public UInt160 BaseToken;
        public UInt160 QuoteToken;
        public BigInteger QuoteScale;
        public BigInteger MinOrderAmount;
        public BigInteger MaxOrderAmount;
        public bool IsPaused;
    }
}
