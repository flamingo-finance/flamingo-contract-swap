using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;

namespace FlamingoSwapOrderBook
{
    public partial class FlamingoSwapOrderBookContract
    {
        /// <summary>
        /// When orderbook status changed
        /// </summary>
        [DisplayName("BookStatusChanged")]
        public static event BookStatusChangedEvent onBookStatusChanged;
        public delegate void BookStatusChangedEvent(UInt160 baseToken, UInt160 quoteToken, uint quoteDecimals, BigInteger minOrderAmount, BigInteger maxOrderAmount, bool isPaused);

        /// <summary>
        /// When order status changed
        /// </summary>
        [DisplayName("OrderStatusChanged")]
        public static event OrderStatusChangedEvent onOrderStatusChanged;
        public delegate void OrderStatusChangedEvent(UInt160 baseToken, UInt160 quoteToken, ByteString id, bool isBuy, UInt160 maker, BigInteger price, BigInteger leftAmount);

        [DisplayName("Fault")]
        public static event FaultEvent onFault;
        public delegate void FaultEvent(string message, params object[] paras);
    }
}