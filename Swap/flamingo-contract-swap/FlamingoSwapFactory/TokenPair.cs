namespace FlamingoSwapFactory
{
    struct TokenPair
    {
        public byte[] Token0;
        public byte[] Token1;
    }

    struct ExchangePair
    {
        public byte[] TokenA;
        public byte[] TokenB;
        public byte[] ExchangePairHash;
    }
}