using Neo;
using Neo.SmartContract.Framework;
using System.Numerics;

namespace FlamingoSwapOrderBook
{
    public struct OrderReceipt
    {
        public UInt160 baseToken;
        public UInt160 quoteToken;
        public ByteString id;
        public ulong time;
        public bool isBuy;
        public UInt160 maker;
        public BigInteger price;
        public BigInteger totalAmount;
        public BigInteger leftAmount;
    }
}