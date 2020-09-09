using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Neo.SmartContract.Framework;

namespace FlamingoSwapRouter
{
    public static class Extensions
    {
        /// <summary>
        /// 调用其它Nep5合约的“transfer”
        /// </summary>
        /// <param name="token"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static bool DynamicTransfer(this byte[] token, byte[] from, byte[] to, BigInteger amount)
        {
            var tokenACall = (Func<string, object[], bool>)token.ToDelegate();
            var args = new object[3];
            args[0] = from;
            args[1] = to;
            args[2] = amount;
            var result = tokenACall("transfer", args);
            return result;
        }



        /// <summary>
        /// 调用其它Nep5合约的“balanceOf”
        /// </summary>
        /// <param name="token"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        public static BigInteger DynamicBalanceOf(this byte[] token, byte[] address)
        {
            var tokenACall = (Func<string, object[], BigInteger>)token.ToDelegate();
            var args = new object[1];
            args[0] = address;
            var result = tokenACall("balanceOf", args);
            return result;
        }



        /// <summary>
        /// 获取tokenA和tokenB的交易对
        /// </summary>
        /// <param name="factory">factory合约地址</param>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        public static byte[] DynamicGetExchangePair(this byte[] factory, byte[] tokenA, byte[] tokenB)
        {
            var tokenACall = (Func<string, object[], byte[]>)factory.ToDelegate();
            var args = new object[2];
            args[0] = tokenA;
            args[1] = tokenB;
            var result = tokenACall("getExchangePair", args);
            return result;
        }



        /// <summary>
        /// 调用pair合约的Mint铸币
        /// </summary>
        /// <param name="pairContract"></param>
        /// <param name="toAddress"></param>
        /// <returns></returns>
        public static BigInteger DynamicMint(this byte[] pairContract, byte[] toAddress)
        {
            var tokenACall = (Func<string, object[], BigInteger>)pairContract.ToDelegate();
            var args = new object[1];
            args[0] = toAddress;
            var result = tokenACall("mint", args);
            return result;
        }


        /// <summary>
        /// 调用pair合约的burn销毁
        /// </summary>
        /// <param name="pairContract"></param>
        /// <param name="toAddress"></param>
        /// <returns></returns>
        public static BigInteger[] DynamicBurn(this byte[] pairContract, byte[] toAddress)
        {
            var tokenACall = (Func<string, object[], BigInteger[]>)pairContract.ToDelegate();
            var args = new object[1];
            args[0] = toAddress;
            var result = tokenACall("burn", args);
            return result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pairContract"></param>
        /// <param name="amount0Out"></param>
        /// <param name="amount1Out"></param>
        /// <param name="toAddress"></param>
        /// <returns></returns>
        public static BigInteger[] DynamicSwap(this byte[] pairContract, BigInteger amount0Out, BigInteger amount1Out, byte[] toAddress)
        {
            var tokenACall = (Func<string, object[], BigInteger[]>)pairContract.ToDelegate();
            var args = new object[3];
            args[0] = amount0Out;
            args[1] = amount1Out;
            args[2] = toAddress;
            var result = tokenACall("swap", args);
            return result;
        }


        /// <summary>
        /// 获取pair合约的两种代币持有量
        /// </summary>
        /// <param name="pairContract"></param>
        /// <returns></returns>
        public static ReservesData DynamicGetReserves(this byte[] pairContract)
        {
            var tokenACall = (Func<string, object[], ReservesData>)pairContract.ToDelegate();
            var args = new object[0];
            var result = tokenACall("getReserves", args);
            return result;
        }


        /// <summary>
        /// byte[] 转为正整数,用于合约地址排序，其它场景勿用 
        /// </summary>
        /// <param name="val">合约地址</param>
        /// <returns></returns>
        public static BigInteger ToUInteger(this byte[] val)
        {
            return val.Concat(new byte[] { 0 }).AsBigInteger();
        }



        /// <summary>
        /// 传入参数转为BigInteger
        /// </summary>
        /// <param name="val">合约地址</param>
        /// <returns></returns>
        [OpCode(OpCode.PUSH0)]
        [OpCode(OpCode.ADD)]
        public static extern BigInteger ToBigInt(this object val);

    }
}
