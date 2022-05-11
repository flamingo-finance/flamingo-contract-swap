using System.Numerics;

namespace FlamingoSwapPair
{
    public struct PriceCumulative
    {
        public BigInteger Price0CumulativeLast;

        public BigInteger Price1CumulativeLast;

        public BigInteger BlockTimestampLast;
    }
}
