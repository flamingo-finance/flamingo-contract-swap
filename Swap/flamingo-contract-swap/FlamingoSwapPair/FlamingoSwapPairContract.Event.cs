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
        public static event OnTransferEvent onTransfer;
        public delegate void OnTransferEvent(UInt160 from, UInt160 to, BigInteger amount);


        /// <summary>
        /// params: message, extend data
        /// </summary>
        [DisplayName("fault")]
        public static event FaultEvent onFault;
        public delegate void FaultEvent(string message, params object[] paras);

    }
}
