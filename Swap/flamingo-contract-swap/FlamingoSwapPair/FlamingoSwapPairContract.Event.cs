using System.ComponentModel;
using System.Numerics;
using Neo;

namespace FlamingoSwapPair
{
    partial class FlamingoSwapPairContract
    {

        /// <summary>
        /// 同步持有量Synced（reserve0，reserve1）
        /// </summary>
        public static event SyncedEvent Synced;
        public delegate void SyncedEvent(BigInteger balance0, BigInteger balance1);

        /// <summary>
        /// 铸币事件 Minted(caller,amount0,amount1,liquidity)
        /// </summary>
        public static event MintedEvent Minted;
        public delegate void MintedEvent(UInt160 caller, BigInteger amount0, BigInteger amount1, BigInteger liquidity);

        /// <summary>
        /// 销毁事件 Burned(caller,liquidity,amount0,amount1,to)
        /// </summary>
        public static event BurnedEvent Burned;
        public delegate void BurnedEvent(UInt160 caller, BigInteger liquidity, BigInteger amount0, BigInteger amount1, UInt160 to);

        /// <summary>
        /// 兑换事件
        /// </summary>
        public static event SwappedEvent Swapped;
        public delegate void SwappedEvent(UInt160 caller, BigInteger amount0In, BigInteger amount1In, BigInteger amount0Out, BigInteger amount1Out, UInt160 to);

        /// <summary>
        /// Deploy事件
        /// </summary>
        public static event DeployedEvent Deployed;
        public delegate void DeployedEvent(UInt160 token0, UInt160 token1);

        [DisplayName("Transfer")]
        public static event OnTransferEvent onTransfer;
        public delegate void OnTransferEvent(UInt160 from, UInt160 to, BigInteger amount);


        /// <summary>
        /// params: message, extend data
        /// </summary>
        [DisplayName("Fault")]
        public static event FaultEvent onFault;
        public delegate void FaultEvent(string message, params object[] paras);

    }
}
