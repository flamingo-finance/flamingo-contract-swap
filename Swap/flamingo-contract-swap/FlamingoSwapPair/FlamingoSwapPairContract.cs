using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
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

        public static BigInteger FIXED = 100_000_000_000_000_000;


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

        [Safe]
        public static UInt160 GetToken0()
        {
            return Token0;
        }

        [Safe]
        public static UInt160 GetToken1()
        {
            return Token1;
        }

        [Safe]
        public static BigInteger GetReserve0()
        {
            var r = ReservePair;
            return r.Reserve0;
        }

        [Safe]
        public static BigInteger GetReserve1()
        {
            var r = ReservePair;
            return r.Reserve1;
        }

        [Safe]
        public static PriceCumulative GetPriceCumulative()
        {
            return Cumulative;
        }

        [Safe]
        public static BigInteger Price0CumulativeLast()
        {
            return Cumulative.Price0CumulativeLast;
        }

        [Safe]
        public static BigInteger Price1CumulativeLast()
        {
            return Cumulative.Price1CumulativeLast;
        }
        #endregion


        public static class EnteredStorage
        {
            public static readonly string mapName = "entered";

            public static void Put(BigInteger value) => new StorageMap(Storage.CurrentContext, mapName).Put(mapName, value);

            public static BigInteger Get()
            {
                var value = new StorageMap(Storage.CurrentContext, mapName).Get(mapName);
                return value is null ? 0 : (BigInteger)value;
            }
        }


        #region Swap

        /// <summary>
        /// 完成兑换，amount0Out 和 amount1Out必需一个为0一个为正数
        /// </summary>
        /// <param name="amount0Out">已经计算好的token0 转出量</param>
        /// <param name="amount1Out">已经计算好的token1 转出量</param>
        /// <param name="toAddress"></param>
        public static bool Swap(BigInteger amount0Out, BigInteger amount1Out, UInt160 toAddress, byte[] data = null)
        {
            //检查是否存在reentered的情况
            Assert(EnteredStorage.Get() == 0, "Re-entered");
            EnteredStorage.Put(1);

            Assert(toAddress.IsAddress(), "Invalid To-Address");
            var caller = Runtime.CallingScriptHash;
            Assert(CheckIsRouter(caller), "Only Router Can Swap");

            var me = Runtime.ExecutingScriptHash;

            Assert(amount0Out >= 0 && amount1Out >= 0, "Invalid AmountOut");
            Assert(amount0Out > 0 || amount1Out > 0, "Invalid AmountOut");

            var r = ReservePair;
            var reserve0 = r.Reserve0;
            var reserve1 = r.Reserve1;

            //转出量小于持有量
            Assert(amount0Out < reserve0 && amount1Out < reserve1, "Insufficient Liquidity");
            //禁止转到token本身的地址
            Assert(toAddress != (UInt160)Token0 && toAddress != (UInt160)Token1 && toAddress != me, "INVALID_TO");

            if (amount0Out > 0)
            {
                //从本合约转出目标token到目标地址
                SafeTransfer(Token0, me, toAddress, amount0Out, data);
            }
            if (amount1Out > 0)
            {
                SafeTransfer(Token1, me, toAddress, amount1Out, data);
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

            //fund fee
            var fundAddress = GetFundAddress();
            if (fundAddress != null)
            {
                if (amount0In > 0)
                {
                    var fee = amount0In * 5 / 10000;
                    if (fee > 0)
                    {
                        SafeTransfer(Token0, me, fundAddress, fee, data);
                        balance0 = DynamicBalanceOf(Token0, me);
                    }
                }
                if (amount1In > 0)
                {
                    var fee = amount1In * 5 / 10000;
                    if (fee > 0)
                    {
                        SafeTransfer(Token1, me, fundAddress, fee, data);
                        balance1 = DynamicBalanceOf(Token1, me);
                    }
                }
            }

            Update(balance0, balance1, r);

            Swapped(caller, amount0In, amount1In, amount0Out, amount1Out, toAddress);
            EnteredStorage.Put(0);
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
            //检查是否存在reentered的情况
            Assert(EnteredStorage.Get() == 0, "Re-entered");
            EnteredStorage.Put(1);
            Assert(toAddress.IsAddress(), "Invalid To-Address");

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

            EnteredStorage.Put(0);
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
            //检查是否存在reentered的情况
            Assert(EnteredStorage.Get() == 0, "Re-entered");
            EnteredStorage.Put(1);
            Assert(toAddress.IsAddress(), "Invalid To-Address");

            var caller = Runtime.CallingScriptHash; //msg.sender
            Assert(CheckIsRouter(caller), "Only Router Can Mint");

            var me = Runtime.ExecutingScriptHash; //address(this)

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
                liquidity = (amount0 * amount1).Sqrt() - MINIMUM_LIQUIDITY;

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

            EnteredStorage.Put(0);
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
            onTransfer(null, toAddress, amount);
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
            onTransfer(fromAddress, null, amount);
        }



        #endregion


        #region SyncUpdate
        /// <summary>
        /// 更新最新持有量（reserve）、区块时间戳(blockTimestamp)
        /// </summary>
        /// <param name="balance0">最新的token0持有量</param>
        /// <param name="balance1">最新的token1持有量</param>
        /// <param name="reserve">旧的reserve数据</param>
        private static void Update(BigInteger balance0, BigInteger balance1, ReservesData reserve)
        {
            BigInteger blockTimestamp = Runtime.Time / 1000 % 4294967296;
            var priceCumulative = Cumulative;
            BigInteger timeElapsed = blockTimestamp - Cumulative.BlockTimestampLast;
            if (timeElapsed > 0 && reserve.Reserve0 != 0 && reserve.Reserve1 != 0)
            {
                priceCumulative.Price0CumulativeLast += reserve.Reserve1 * FIXED * timeElapsed / reserve.Reserve0;
                priceCumulative.Price1CumulativeLast += reserve.Reserve0 * FIXED * timeElapsed / reserve.Reserve1;
                priceCumulative.BlockTimestampLast = blockTimestamp;
                Cumulative = priceCumulative;
            }
            reserve.Reserve0 = balance0;
            reserve.Reserve1 = balance1;

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
                    return new ReservesData() { Reserve0 = 0, Reserve1 = 0 };
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

        private static PriceCumulative Cumulative
        {
            get
            {
                var val = StorageGet(nameof(Cumulative));
                if (val is null || val.Length == 0)
                {
                    return new PriceCumulative() { Price0CumulativeLast = 0, Price1CumulativeLast = 0, BlockTimestampLast = 0 };
                }
                var b = (PriceCumulative)StdLib.Deserialize(val);
                return b;
            }
            set
            {
                var val = StdLib.Serialize(value);
                StoragePut(nameof(Cumulative), val);
            }
        }

        public static object GetReserves()
        {
            return ReservePair;
        }

        #endregion
    }
}
