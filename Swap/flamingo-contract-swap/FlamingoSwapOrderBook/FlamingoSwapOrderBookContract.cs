using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;
using System.ComponentModel;
using System.Numerics;

namespace FlamingoSwapOrderBook
{
    [DisplayName("FlamingoSwapOrderBook")]
    [ManifestExtra("Author", "Flamingo Finance")]
    [ManifestExtra("Email", "developer@flamingo.finance")]
    [ManifestExtra("Description", "This is a Flamingo Contract")]
    [ContractPermission("*")]
    public partial class FlamingoSwapOrderBookContract : SmartContract
    {
        public static BigInteger[] DealOrder(UInt160 taker, ByteString orderID, BigInteger amount)
        {
            Assert(Runtime.CheckWitness(taker), "No Authorization");
            return null;
        }

        public static BigInteger[] DealOrders(UInt160 tokenA, UInt160 tokenB, UInt160 taker, bool isBuy, BigInteger amount, BigInteger price, ByteString[] orderIDs)
        {
            Assert(Runtime.CheckWitness(taker), "No Authorization");
            return null;
        }

        public static bool DealLimitOrder(UInt160 tokenA, UInt160 tokenB, UInt160 taker, bool isBuy, BigInteger amount, BigInteger price, ByteString[] orderIDs)
        {
            Assert(tokenA.IsAddress() && tokenB.IsAddress() && amount > 0 && price > 0, "Invalid Parameters");
            Assert(Runtime.CheckWitness(taker), "No Authorization");

            return false;
        }

        public static bool DealMarketOrder(UInt160 tokenA, UInt160 tokenB, UInt160 taker, bool isBuy, BigInteger amount, BigInteger amountOutMin, ByteString[] orderIDs)
        {
            Assert(Runtime.CheckWitness(taker), "No Authorization");

            return false;
        }

        [Safe]
        public static BookInfo GetBookInfo(UInt160 tokenA, UInt160 tokenB)
        {
            return GetBook(GetPairKey(tokenA, tokenB));
        }

        [Safe]
        public static LimitOrder[] GetOrdersOnPage(BigInteger pageIndex)
        {
            var results = new LimitOrder[0];
            var iterator = GetOrdersByPage(pageIndex);
            while (iterator.Next()) Append(results, (LimitOrder)iterator.Value);
            return results;
        }

        /// <summary>
        /// 根据要达到的限价簿价格，计算资金池需要输入的Token量
        /// </summary>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="quoteScale"></param>
        /// <param name="reserveIn"></param>
        /// <param name="reserveOut"></param>
        public static BigInteger GetAMMAmountInTillPrice(bool isBuy, BigInteger price, BigInteger quoteScale, BigInteger reserveIn, BigInteger reserveOut)
        {
            Assert(price > 0 && quoteScale > 0 && reserveIn > 0 && reserveOut > 0, "Parameter Invalid");
            var amountIn = BigInteger.Pow(reserveIn, 2) * 9000000;
            if (isBuy) amountIn += reserveIn * reserveOut * price * 3988000000000 / quoteScale;
            else amountIn += reserveIn * reserveOut * quoteScale * 3988000000000 / price;
            return (amountIn.Sqrt() - reserveIn * 1997000) / 1994000;
        }

        public static BigInteger GetAMMAmountInTillPriceWithFundFee(bool isBuy, BigInteger price, BigInteger quoteScale, BigInteger reserveIn, BigInteger reserveOut)
        {
            Assert(price > 0 && quoteScale > 0 && reserveIn > 0 && reserveOut > 0, "Parameter Invalid");
            var amountIn = BigInteger.Pow(reserveIn, 2) * 6250000;
            if (isBuy) amountIn += reserveIn * reserveOut * price * 3986006000000 / quoteScale;
            else amountIn += reserveIn * reserveOut * quoteScale * 3986006000000 / price;
            return (amountIn.Sqrt() - reserveIn * 1996500) / 1993003;
        }

        /// <summary>
        /// 根据输入A获取资金池兑换B的量（扣除千分之三手续费）
        /// </summary>
        /// <param name="amountIn"></param>
        /// <param name="reserveIn"></param>
        /// <param name="reserveOut"></param>
        /// <returns></returns>
        public static BigInteger GetAMMAmountOut(BigInteger amountIn, BigInteger reserveIn, BigInteger reserveOut)
        {
            Assert(amountIn >= 0 && reserveIn > 0 && reserveOut > 0, "AmountIn Must >= 0");
            var amountInWithFee = amountIn * 997;
            var numerator = amountInWithFee * reserveOut;
            var denominator = reserveIn * 1000 + amountInWithFee;
            var amountOut = numerator / denominator;
            return amountOut;
        }

        /// <summary>
        /// 根据要兑换的输出量B，计算资金池需要输入的A实际量（已计算千分之三手续费）
        /// </summary>
        /// <param name="amountOut"></param>
        /// <param name="reserveIn"></param>
        /// <param name="reserveOut"></param>
        /// <returns></returns>
        public static BigInteger GetAMMAmountIn(BigInteger amountOut, BigInteger reserveIn, BigInteger reserveOut)
        {
            Assert(amountOut >= 0 && reserveIn > 0 && reserveOut > 0, "AmountOut Must >= 0");
            var numerator = reserveIn * amountOut * 1000;
            var denominator = (reserveOut - amountOut) * 997;
            var amountIn = (numerator / denominator) + 1;
            return amountIn;
        }

        /// <summary>
        /// 接受nep17 token必备方法
        /// SwapTokenInForTokenOut
        /// </summary>
        /// <param name="from"></param>
        /// <param name="amount"></param>
        /// <param name="data"></param>
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {

        }

        /// <summary>
        /// 根据计算好的输入和输出，使用资金池进行兑换
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="tokenIn"></param>
        /// <param name="tokenOut"></param>
        /// <param name="amountIn"></param>
        /// <param name="amountOut"></param>
        private static void SwapAMM(UInt160 sender, UInt160 tokenIn, UInt160 tokenOut, BigInteger amountIn, BigInteger amountOut)
        {
            //转入tokenIn
            var pairContract = GetExchangePairWithAssert(tokenIn, tokenOut);
            SafeTransfer(tokenIn, sender, pairContract, amountIn);

            //判定要转出的是token0还是token1
            BigInteger amount0Out = 0;
            BigInteger amount1Out = 0;
            if (tokenIn.ToUInteger() < tokenOut.ToUInteger()) amount1Out = amountOut;
            else amount0Out = amountOut;

            //转出tokenOut
            SwapOut(pairContract, amount0Out, amount1Out, sender);
        }
    }
}
