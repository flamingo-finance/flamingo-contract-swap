using System.ComponentModel;
using System.Numerics;
using Neo;

namespace ProxyTemplate
{
    partial class ProxyTemplateContract
    {
        [DisplayName("Deposit")]
        public static event DepositEvent onDeposit;
        public delegate void DepositEvent(UInt160 token, UInt160 from, BigInteger amount);

        [DisplayName("Withdraw")]
        public static event WithdrawEvent onWithdraw;
        public delegate void WithdrawEvent(UInt160 token, UInt160 to, BigInteger amount);

        [DisplayName("Add")]
        public static event AddEvent onAdd;
        public delegate void AddEvent(BigInteger amount0, BigInteger amount1);

        [DisplayName("Remove")]
        public static event RemoveEvent onRemove;
        public delegate void RemoveEvent(BigInteger amount0, BigInteger amount1);

        [DisplayName("Fault")]
        public static event FaultEvent onFault;
        public delegate void FaultEvent(string message, params object[] paras);
    }
}
