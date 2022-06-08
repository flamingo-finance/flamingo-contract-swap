using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;

namespace FlamingoSwapOrderBook
{
    public partial class FlamingoSwapOrderBookContract
    {
        /// <summary>
        /// When register orderbook
        /// </summary>
        [DisplayName("RegisterBook")]
        public static event RegisterBookEvent onRegisterBook;
        public delegate void RegisterBookEvent(UInt160 baseToken, UInt160 quoteToken, uint quoteDecimals);

        /// <summary>
        /// When remove orderbook
        /// </summary>
        [DisplayName("RemoveBook")]
        public static event RemoveBookEvent onRemoveBook;
        public delegate void RemoveBookEvent(UInt160 baseToken, UInt160 quoteToken);

        /// <summary>
        /// When add order
        /// </summary>
        [DisplayName("OrderStatusChanged")]
        public static event OrderStatusChangedEvent onOrderStatusChanged;
        public delegate void OrderStatusChangedEvent(UInt160 baseToken, UInt160 quoteToken, ByteString id, bool isBuy, UInt160 maker, BigInteger price, BigInteger leftAmount);

        [DisplayName("Fault")]
        public static event FaultEvent onFault;
        public delegate void FaultEvent(string message, params object[] paras);
    }
}