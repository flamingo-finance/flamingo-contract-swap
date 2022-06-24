using System.ComponentModel;

namespace FlamingoSwapAggregator
{
    partial class FlamingoSwapAggregatorContract
    {
        /// <summary>
        /// params: message, extend data
        /// </summary>
        [DisplayName("Fault")]
        public static event FaultEvent onFault;
        public delegate void FaultEvent(string message, params object[] paras);

    }
}
