using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Neo;
using Neo.SmartContract.Framework;

namespace FlamingoSwapRouter
{
    public static class Extensions
    {
        /// <summary>
        /// 调用pair合约的Mint铸币
        /// </summary>
        /// <param name="pairContract"></param>
        /// <param name="toAddress"></param>
        /// <returns></returns>
        public static BigInteger DynamicMint(this byte[] pairContract, byte[] toAddress)
        {
            return ((Func<string, object[], BigInteger>)pairContract.ToDelegate())("mint", new object[] { toAddress });
        }

        /// <summary>
        /// 调用pair合约的Mint铸币
        /// </summary>
        /// <param name="pairContract"></param>
        /// <param name="toAddress"></param>
        /// <returns></returns>
        public static BigInteger DynamicMint(this UInt160 pairContract, UInt160 toAddress)
        {
            return ((Func<string, object[], BigInteger>)((byte[])pairContract).ToDelegate())("mint", new object[] { toAddress });
        }

        /// <summary>
        /// 调用pair合约的burn销毁
        /// </summary>
        /// <param name="pairContract"></param>
        /// <param name="toAddress"></param>
        /// <returns></returns>
        public static BigInteger[] DynamicBurn(this byte[] pairContract, byte[] toAddress)
        {
            return ((Func<string, object[], BigInteger[]>)pairContract.ToDelegate())("burn", new object[] { toAddress });
        }


        /// <summary>
        /// 调用pair合约的burn销毁
        /// </summary>
        /// <param name="pairContract"></param>
        /// <param name="toAddress"></param>
        /// <returns></returns>
        public static BigInteger[] DynamicBurn(this UInt160 pairContract, UInt160 toAddress)
        {
            return ((Func<string, object[], BigInteger[]>)((byte[])pairContract).ToDelegate())("burn", new object[] { toAddress });
        }


        /// <summary>
        /// byte[] 转为正整数,用于合约地址排序，其它场景勿用 
        /// </summary>
        /// <param name="val">合约地址</param>
        /// <returns></returns>
        [OpCode(OpCode.PUSHDATA1, "0100")]
        [OpCode(OpCode.CAT)]
        public static extern BigInteger ToUInteger(this byte[] val);


        /// <summary>
        /// byte[] 转为正整数,用于合约地址排序，其它场景勿用 
        /// </summary>
        /// <param name="val">合约地址</param>
        /// <returns></returns>
        [OpCode(OpCode.PUSHDATA1, "0100")]
        [OpCode(OpCode.CAT)]
        public static extern BigInteger ToUInteger(this UInt160 val);



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
