using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace FlamingoSwapPair
{
    [DisplayName("Flamingo Swap-Pair Contract")]
    [ManifestExtra("Author", "Flamingo Finance")]
    [ManifestExtra("Email", "developer@flamingo.finance")]
    [ManifestExtra("Description", "This is a Flamingo Contract")]
    [SupportedStandards("NEP-17")]
    [ContractPermission("*")]//avoid native contract hash change
    public partial class FlamingoSwapPairContract : SmartContract
    {

        /// <summary>
        /// https://uniswap.org/docs/v2/protocol-overview/smart-contracts/#minimum-liquidity
        /// </summary>
        const long MINIMUM_LIQUIDITY = 1000;

        #region 通知

        /// <summary>
        /// 同步持有量Synced（reserve0，reserve1）
        /// </summary>
        private static event SyncedEvent Synced;
        private delegate void SyncedEvent(BigInteger balance0, BigInteger balance1);

        /// <summary>
        /// 铸币事件 Minted(caller,amount0,amount1,liquidity)
        /// </summary>
        private static event MintedEvent Minted;
        private delegate void MintedEvent(UInt160 caller, BigInteger amount0, BigInteger amount1, BigInteger liquidity);

        /// <summary>
        /// 销毁事件 Burned(caller,liquidity,amount0,amount1,to)
        /// </summary>
        private static event BurnedEvent Burned;
        private delegate void BurnedEvent(UInt160 caller, BigInteger liquidity, BigInteger amount0, BigInteger amount1, UInt160 to);

        /// <summary>
        /// 兑换事件
        /// </summary>
        private static event SwappedEvent Swapped;
        private delegate void SwappedEvent(UInt160 caller, BigInteger amount0In, BigInteger amount1In, BigInteger amount0Out, BigInteger amount1Out, UInt160 to);

        /// <summary>
        /// Deploy事件
        /// </summary>
        private static event DeployedEvent Deployed;
        private delegate void DeployedEvent(UInt160 token0, UInt160 token1);

        [DisplayName("Transfer")]
        public static event OnTransferEvent OnTransfer;
        public delegate void OnTransferEvent(UInt160 from, UInt160 to, BigInteger amount);
        #endregion


        //public static object Main(string method, object[] args)
        //{
        //    if (Runtime.Trigger == TriggerType.Verification)
        //    {
        //        return Runtime.CheckWitness(GetAdmin());
        //    }
        //    else if (Runtime.Trigger == TriggerType.Application)
        //    {
        //        //合约调用时，等价以太坊的msg.sender
        //        //直接调用时，此处为 tx.Script.ToScriptHash();
        //        var msgSender = ExecutionEngine.CallingScriptHash;

        //        if (method == "getReserves") return ReservePair;

        //        if (method == "mint") return Mint(msgSender, (byte[])args[0]);//msgSender应当为router

        //        if (method == "burn") return Burn(msgSender, (byte[])args[0]);//msgSender应当为router

        //        if (method == "swap") return Swap(msgSender, (BigInteger)args[0], (BigInteger)args[1], (byte[])args[2]);

        //        if (method == "transfer") return Transfer((byte[])args[0], (byte[])args[1], (BigInteger)args[2], msgSender);

        //        if (method == "balanceOf") return BalanceOf((byte[])args[0]);

        //        if (method == "decimals") return Decimals();

        //        if (method == "name") return Name();

        //        if (method == "symbol") return Symbol();

        //        if (method == "supportedStandards") return SupportedStandards();

        //        if (method == "totalSupply") return GetTotalSupply();

        //        if (method == "getToken0") return Token0;

        //        if (method == "getToken1") return Token1;

        //        if (method == "getAdmin") return GetAdmin();

        //        if (method == "setAdmin") return SetAdmin((byte[])args[0]);

        //        if (method == "getWhiteListContract") return GetWhiteListContract();

        //        if (method == "setWhiteListContract") return SetWhiteListContract((byte[])args[0]);

        //        if (method == "checkIsRouter") return CheckIsRouter((byte[])args[0]);

        //        if (method == "upgrade")
        //        {
        //            Assert(args.Length == 9, "upgrade: args.Length != 9");
        //            byte[] script = (byte[])args[0];
        //            byte[] plist = (byte[])args[1];
        //            byte rtype = (byte)args[2];
        //            ContractPropertyState cps = (ContractPropertyState)args[3];
        //            string name = (string)args[4];
        //            string version = (string)args[5];
        //            string author = (string)args[6];
        //            string email = (string)args[7];
        //            string description = (string)args[8];
        //            return Upgrade(script, plist, rtype, cps, name, version, author, email, description);
        //        }

        //    }
        //    return false;
        //}


        /// <summary>
        /// 合约初始化
        /// </summary>
        /// <param name="data"></param>
        /// <param name="update"></param>
        public static void _deploy(object data, bool update)
        {
            if (TokenA.ToUInteger() < TokenB.ToUInteger())
            {
                Token0 = TokenA;
                Token1 = TokenB;
            }
            else
            {
                Token0 = TokenB;
                Token1 = TokenA;
            }
            Deployed(Token0, Token1);
        }



        #region Token0,Token1


        /// <summary>
        /// Token 0 地址(Token0放置合约hash小的token)
        /// </summary>
        static UInt160 Token0
        {
            get => (UInt160)StorageGet("token0");
            set => StoragePut("token0", value);
        }


        /// <summary>
        ///  Token 1 地址
        /// </summary>
        static UInt160 Token1
        {
            get => (UInt160)StorageGet("token1");
            set => StoragePut("token1", value);
        }


        public static UInt160 GetToken0()
        {
            return Token0;
        }

        public static UInt160 GetToken1()
        {
            return Token1;
        }

        #endregion


        #region Swap

        /// <summary>
        /// 完成兑换，amount0Out 和 amount1Out必需一个为0一个为正数
        /// </summary>
        /// <param name="amount0Out">已经计算好的token0 转出量</param>
        /// <param name="amount1Out">已经计算好的token1 转出量</param>
        /// <param name="toAddress"></param>
        public static bool Swap(BigInteger amount0Out, BigInteger amount1Out, UInt160 toAddress)
        {
            var caller = Runtime.CallingScriptHash;

            Assert(CheckIsRouter(caller), "Only Router Can Swap");

            var me = Runtime.ExecutingScriptHash;

            //转出量必需一个为0一个为正数
            Assert(amount0Out * amount1Out == 0 && (amount0Out > 0 || amount1Out > 0), "Invalid AmountOut");
            var r = ReservePair;
            var reserve0 = r.Reserve0;
            var reserve1 = r.Reserve1;

            //转出量小于持有量
            Assert(amount0Out < reserve0 && amount1Out < reserve1, "Insufficient Liquidity");
            //禁止转到token本身的地址
            Assert(toAddress != (UInt160)Token0 && toAddress != (UInt160)Token1, "INVALID_TO");
            if (amount0Out > 0)
            {
                //从本合约转出目标token到目标地址
                SafeTransfer(Token0, me, toAddress, amount0Out);
            }

            if (amount1Out > 0)
            {
                SafeTransfer(Token1, me, toAddress, amount1Out);
            }


            BigInteger balance0 = DynamicBalanceOf(Token0, me);
            BigInteger balance1 = DynamicBalanceOf(Token1, me);


            //计算转入的token量：转入转出后token余额balance>reserve，代表token转入，计算结果为正数
            var amount0In = balance0 > (reserve0 - amount0Out) ? balance0 - (reserve0 - amount0Out) : 0;
            var amount1In = balance1 > (reserve1 - amount1Out) ? balance1 - (reserve1 - amount1Out) : 0;
            //swap 时至少有一个转入
            Assert(amount0In > 0 || amount1In > 0, "Invalid AmountIn");

            //amountIn 收取千分之三手续费
            var balance0Adjusted = balance0 * 1000 - amount0In * 3;
            var balance1Adjusted = balance1 * 1000 - amount1In * 3;

            //恒定积
            Assert(balance0Adjusted * balance1Adjusted >= reserve0 * reserve1 * 1_000_000, "K");

            Update(balance0, balance1, r);

            Swapped(caller, amount0In, amount1In, amount0Out, amount1Out, toAddress);
            return true;
        }


        #endregion

        #region Burn and Mint

        /// <summary>
        /// 销毁liquidity代币，并转出等量的token0和token1到toAddress
        /// 需要事先将用户持有的liquidity转入本合约才可以调此方法
        /// </summary>
        /// <param name="toAddress"></param>
        /// <returns></returns>
        public static object Burn(UInt160 toAddress)
        {
            var caller = Runtime.CallingScriptHash;
            Assert(CheckIsRouter(caller), "Only Router Can Burn");
            var me = Runtime.ExecutingScriptHash;
            var r = ReservePair;

            var balance0 = DynamicBalanceOf(Token0, me);
            var balance1 = DynamicBalanceOf(Token1, me);
            var liquidity = BalanceOf(me);

            var totalSupply = TotalSupply();
            var amount0 = liquidity * balance0 / totalSupply;//要销毁(转出)的token0额度：me持有的token0 * (me持有的me token/me token总量）
            var amount1 = liquidity * balance1 / totalSupply;

            Assert(amount0 > 0 && amount1 > 0, "Insufficient LP Burned");
            BurnToken(me, liquidity);

            //从本合约转出token
            SafeTransfer(Token0, me, toAddress, amount0);
            SafeTransfer(Token1, me, toAddress, amount1);

            balance0 = DynamicBalanceOf(Token0, me);
            balance1 = DynamicBalanceOf(Token1, me);

            Update(balance0, balance1, r);

            Burned(caller, liquidity, amount0, amount1, toAddress);

            return new BigInteger[]
            {
                amount0,
                amount1,
            };
        }


        /// <summary>
        /// 铸造代币，此方法应该由router在AddLiquidity时调用
        /// </summary>
        /// <param name="toAddress"></param>
        /// <returns>返回本次铸币量</returns>
        public static BigInteger Mint(UInt160 toAddress)
        {
            var caller = Runtime.CallingScriptHash;
            Assert(CheckIsRouter(caller), "Only Router Can Mint");

            var me = Runtime.ExecutingScriptHash;

            var r = ReservePair;
            var reserve0 = r.Reserve0;
            var reserve1 = r.Reserve1;
            var balance0 = DynamicBalanceOf(Token0, me);
            var balance1 = DynamicBalanceOf(Token1, me);

            var amount0 = balance0 - reserve0;//token0增量
            var amount1 = balance1 - reserve1;//token1增量

            var totalSupply = TotalSupply();

            BigInteger liquidity;
            if (totalSupply == 0)
            {
                liquidity = Sqrt(amount0 * amount1) - MINIMUM_LIQUIDITY;
                //第一笔注入资金过少，liquidity为负数，整个合约执行将中断回滚

                MintToken(UInt160.Zero, MINIMUM_LIQUIDITY);// permanently lock the first MINIMUM_LIQUIDITY tokens,永久锁住第一波发行的 MINIMUM_LIQUIDITY token
            }
            else
            {
                var liquidity0 = amount0 * totalSupply / reserve0;
                var liquidity1 = amount1 * totalSupply / reserve1;
                liquidity = liquidity0 > liquidity1 ? liquidity1 : liquidity0;
            }

            Assert(liquidity > 0, "Insufficient LP Minted");
            MintToken(toAddress, liquidity);

            Update(balance0, balance1, r);

            Minted(caller, amount0, amount1, liquidity);
            return liquidity;
        }


        /// <summary>
        /// 铸币（不校验签名），内部方法禁止外部直接调用
        /// </summary>
        /// <param name="toAddress">接收新铸造的币的账号</param>
        /// <param name="amount">铸造量</param>
        private static void MintToken(UInt160 toAddress, BigInteger amount)
        {
            AssetStorage.Increase(toAddress, amount);
            TotalSupplyStorage.Increase(amount);
            OnTransfer(null, toAddress, amount);
        }

        /// <summary>
        /// 物理销毁token（不校验签名），内部方法禁止外部直接调用
        /// </summary>
        /// <param name="fromAddress">token的持有地址</param>
        /// <param name="amount">销毁的token量</param>
        private static void BurnToken(UInt160 fromAddress, BigInteger amount)
        {
            AssetStorage.Reduce(fromAddress, amount);
            TotalSupplyStorage.Reduce(amount);
            OnTransfer(fromAddress, null, amount);
        }



        #endregion


        #region SyncUpdate


        ///// <summary>
        ///// 更新最新持有量（reserve）、价格累计量（price0CumulativeLast）、区块时间戳(blockTimestamp)
        ///// </summary>
        ///// <param name="balance0">最新的token0持有量</param>
        ///// <param name="balance1">最新的token1持有量</param>
        ///// <param name="reserve0"></param>
        ///// <param name="reserve1"></param>
        //private static void Update(BigInteger balance0, BigInteger balance1, BigInteger reserve0, BigInteger reserve1)
        //{
        //    //require(balance0 <= uint112(-1) && balance1 <= uint112(-1), 'UniswapV2: OVERFLOW');
        //    var r = ReservePair;
        //    var blockTimestamp = Runtime.Time;
        //    //var blockTimestampLast = r.BlockTimestampLast;
        //    //var timeElapsed = blockTimestamp - blockTimestampLast;
        //    //if (timeElapsed > 0 && reserve0 != 0 && reserve1 != 0)
        //    //{
        //    //    //todo:原始算法??
        //    //    //price0CumulativeLast += (total1 * Q112) / total0 * timeElapsed;
        //    //    // * never overflows, and + overflow is desired
        //    //    // price0CumulativeLast += uint(UQ112x112.encode(_reserve1).uqdiv(_reserve0)) * timeElapsed;
        //    //    // price1CumulativeLast += uint(UQ112x112.encode(_reserve0).uqdiv(_reserve1)) * timeElapsed;
        //    //    var price0CumulativeLast = GetPrice0CumulativeLast() + reserve1 / reserve0 * timeElapsed;
        //    //    var price1CumulativeLast = GetPrice1CumulativeLast() + reserve0 / reserve1 * timeElapsed;

        //    //    SetPrice0CumulativeLast(price0CumulativeLast);
        //    //    SetPrice1CumulativeLast(price1CumulativeLast);
        //    //}


        //    r.Reserve0 = balance0;
        //    r.Reserve1 = balance1;
        //    r.BlockTimestampLast = blockTimestamp;
        //    //优化写入次数
        //    ReservePair = r;

        //    Synced(balance0, balance1);
        //}



        /// <summary>
        /// 更新最新持有量（reserve）、区块时间戳(blockTimestamp)
        /// </summary>
        /// <param name="balance0">最新的token0持有量</param>
        /// <param name="balance1">最新的token1持有量</param>
        /// <param name="reserve">旧的reserve数据</param>
        private static void Update(BigInteger balance0, BigInteger balance1, ReservesData reserve)
        {
            reserve.Reserve0 = balance0;
            reserve.Reserve1 = balance1;
            reserve.BlockTimestampLast = Runtime.Time;
            //优化写入次数
            //SetReserves(reserve);
            ReservePair = reserve;
            Synced(balance0, balance1);
        }

        #endregion

        #region Reserve读写



        /// <summary>
        /// Reserve读写，节约gas
        /// </summary>
        private static ReservesData ReservePair
        {
            get
            {
                var val = StorageGet(nameof(ReservePair));
                if (val is null || val.Length == 0)
                {
                    return new ReservesData() { Reserve0 = 0, Reserve1 = 0, BlockTimestampLast = 0 };
                }
                var b = (ReservesData)StdLib.Deserialize(val);
                return b;
            }
            set
            {

                var val = StdLib.Serialize(value);
                StoragePut(nameof(ReservePair), val);
            }
        }

        public static object GetReserves()
        {
            return ReservePair;
        }

        #endregion

        #region PriceCumulativeLast累计价格


        ///// <summary>
        ///// 累计价格存储Key
        ///// </summary>
        //private const string Price0CumulativeLastStoreKey = "Price0CumulativeLast";
        //private const string Price1StoreKey = "Price1CumulativeLast";


        ///// <summary>
        ///// 获取token0累计价格
        ///// </summary>
        ///// <returns></returns>
        //private static BigInteger GetPrice0CumulativeLast()
        //{
        //    return Storage.Get(Price0CumulativeLastStoreKey).AsBigInteger();
        //}


        ///// <summary>
        ///// 设置token0累计价格
        ///// </summary>
        ///// <param name="price0CumulativeLast"></param>
        ///// <returns></returns>
        //private static bool SetPrice0CumulativeLast(BigInteger price0CumulativeLast)
        //{
        //    Storage.Put(Price0CumulativeLastStoreKey, price0CumulativeLast);
        //    return true;
        //}



        ///// <summary>
        ///// 获取token1累计价格
        ///// </summary>
        ///// <returns></returns>
        //private static BigInteger GetPrice1CumulativeLast()
        //{
        //    return Storage.Get(Price1StoreKey).AsBigInteger();
        //}


        ///// <summary>
        ///// 设置token1累计价格
        ///// </summary>
        ///// <param name="price1CumulativeLast"></param>
        ///// <returns></returns>
        //private static bool SetPrice1CumulativeLast(BigInteger price1CumulativeLast)
        //{
        //    Storage.Put(Price1StoreKey, price1CumulativeLast);
        //    return true;
        //}

        #endregion

        #region K值


        ///// <summary>
        ///// 获取记录的KLast（reserve0 * reserve1,恒定积）
        ///// </summary>
        ///// <returns></returns>
        //private static BigInteger GetKLast()
        //{
        //    return Storage.Get("KLast").AsBigInteger();
        //}


        ///// <summary>
        ///// 记录的KLast(reserve0 * reserve1,恒定积)
        ///// </summary>
        ///// <param name="kLast"></param>
        ///// <returns></returns>
        //private static bool SetKLast(BigInteger kLast)
        //{
        //    Storage.Put("KLast", kLast);
        //    return true;
        //}

        #endregion

    }
}
