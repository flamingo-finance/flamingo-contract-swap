using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;
using System;
using System.Numerics;

namespace FlamingoSwapOrderBook
{
    public partial class FlamingoSwapOrderBookContract
    {

        [OpCode(OpCode.APPEND)]
        private static extern void Append<T>(T[] array, T newItem);

        /// <summary>
        /// 断言
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        /// <summary>
        /// 断言
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        /// <param name="data"></param>
        private static void Assert(bool condition, string message, params object[] data)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        /// <summary>
        /// 安全查询交易对，查不到立即中断合约执行
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        public static UInt160 GetExchangePairWithAssert(UInt160 tokenA, UInt160 tokenB)
        {
            Assert(tokenA.IsValid && tokenB.IsValid, "Invalid A or B Address");
            var pairContract = (byte[])Contract.Call(Factory, "getExchangePair", CallFlags.ReadOnly, new object[] { tokenA, tokenB });
            Assert(pairContract != null && pairContract.Length == 20, "PairContract Not Found", tokenA, tokenB);
            return (UInt160)pairContract;
        }

        /// <summary>
        /// 查询TokenA,TokenB交易对合约的里的持有量并按A、B顺序返回
        /// </summary>
        /// <param name="pairContract"></param>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        public static BigInteger[] GetReserves(UInt160 pairContract, UInt160 tokenA, UInt160 tokenB)
        {
            var reserveData = (ReservesData)Contract.Call(pairContract, "getReserves", CallFlags.ReadOnly, new object[] { });
            return tokenA.ToUInteger() < tokenB.ToUInteger() ? new BigInteger[] { reserveData.Reserve0, reserveData.Reserve1 } : new BigInteger[] { reserveData.Reserve1, reserveData.Reserve0 };
        }

        /// <summary>
        /// 查询交易对合约是否抽取fundfee
        /// </summary>
        /// <param name="pairContract"></param>
        /// <returns></returns>
        public static bool HasFundAddress(UInt160 pairContract)
        {
            return (byte[])Contract.Call(pairContract, "getFundAddress", CallFlags.ReadOnly, new object[] { }) != null;
        }

        /// <summary>
        /// 向资金池转发兑出请求
        /// </summary>
        /// <param name="pairContract"></param>
        /// <param name="amount0Out"></param>
        /// <param name="amount1Out"></param>
        /// <param name="toAddress"></param>
        private static void SwapOut(UInt160 pairContract, BigInteger amount0Out, BigInteger amount1Out, UInt160 toAddress)
        {
            Contract.Call(pairContract, "swap", CallFlags.All, new object[] { amount0Out, amount1Out, toAddress, null });
        }

        /// <summary>
        /// Transfer NEP-5 tokens from user
        /// </summary>
        /// <param name="token"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private static void SafeTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount, byte[] data = null)
        {
            try
            {
                var result = (bool)Contract.Call(token, "transfer", CallFlags.All, new object[] { from, to, amount, data });
                Assert(result, "Transfer Fail in OrderBook", token);
            }
            catch (Exception)
            {
                Assert(false, "Transfer Error in OrderBook", token);
            }
        }

        /// <summary>
        /// Transfer NEP-5 tokens from contract
        /// </summary>
        /// <param name="token"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private static void RequestTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount, byte[] data = null)
        {
            try
            {
                var balanceBefore = (BigInteger)Contract.Call(token, "balanceOf", CallFlags.ReadOnly, new object[] { to });
                var result = (bool)Contract.Call(from, "approvedTransfer", CallFlags.All, new object[] { token, to, amount, data });
                var balanceAfter = (BigInteger)Contract.Call(token, "balanceOf", CallFlags.ReadOnly, new object[] { to });
                Assert(result, "Transfer Not Approved in OrderBook", token);
                Assert(balanceAfter == balanceBefore + amount, "Unexpected Transfer in OrderBook", token);
            }
            catch (Exception)
            {
                Assert(false, "Transfer Error in OrderBook", token);
            }
        }
    }
}
