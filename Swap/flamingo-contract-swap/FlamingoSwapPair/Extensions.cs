﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Neo;
using Neo.SmartContract.Framework;

namespace FlamingoSwapPair
{
    public static class Extensions
    {
        /// <summary>
        /// uint160 转为正整数,用于合约地址排序，其它场景勿用 
        /// </summary>
        /// <param name="val">合约地址</param>
        /// <returns></returns>
        [OpCode(OpCode.PUSHDATA1, "0100")]
        [OpCode(OpCode.CAT)]
        [OpCode(OpCode.CONVERT, "21")]
        public static extern BigInteger ToUInteger(this UInt160 val);
    }
}
