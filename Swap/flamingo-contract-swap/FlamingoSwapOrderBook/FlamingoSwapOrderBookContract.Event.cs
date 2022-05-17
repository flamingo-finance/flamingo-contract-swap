using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using System;
using System.ComponentModel;

namespace FlamingoSwapOrderBook
{
    public partial class FlamingoSwapOrderBookContract : SmartContract
    {
        [DisplayName("Fault")]
        public static event FaultEvent onFault;
        public delegate void FaultEvent(string message, params object[] paras);
    }
}