using System.ComponentModel;
using System.Numerics;
using Neo;

namespace FlamingoSwapOrderBook
{
    public partial class FlamingoSwapOrderBookContract
    {
        /// <summary>
        /// When register orderbook
        /// </summary>
        public static event RegisterBookEvent onRegisterBook;
        public delegate void RegisterBookEvent(UInt160 baseToken, UInt160 quoteToken, uint quoteDecimals);

        /// <summary>
        /// When remove orderbook
        /// </summary>
        // public static event RemoveBookEvent onRemoveBook;
        // public delegate void RemoveBookEvent(UInt160 baseToken, UInt160 quoteToken);

        /// <summary>
        /// When deal order
        /// </summary>
        public static event DealOrderEvent onDealOrder;
        public delegate void DealOrderEvent(uint id, BigInteger price, BigInteger amount, BigInteger leftAmount);

        /// <summary>
        /// When add order
        /// </summary>
        public static event AddOrderEvent onAddOrder;
        public delegate void AddOrderEvent(UInt160 tokenFrom, UInt160 tokenTo, UInt160 sender, BigInteger price, BigInteger amount);

        /// <summary>
        /// When cancel order
        /// </summary>
        public static event CancelOrderEvent onCancelOrder;
        public delegate void CancelOrderEvent(uint id, BigInteger leftAmount);

        [DisplayName("Fault")]
        public static event FaultEvent onFault;
        public delegate void FaultEvent(string message, params object[] paras);
    }
}