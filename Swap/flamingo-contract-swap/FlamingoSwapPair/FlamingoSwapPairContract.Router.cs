using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Neo.SmartContract.Framework.Services.Neo;

namespace FlamingoSwapPair
{
    partial class FlamingoSwapPairContract
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="to"></param>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="tokenADesired">The amount of tokenA to add as liquidity if the B/A price is <= amountBDesired/amountADesired (A depreciates).</param>
        /// <param name="tokenBDesired"></param>
        /// <param name="amountAMin">Bounds the extent to which the B/A price can go up before the transaction reverts. Must be <= amountADesired.</param>
        /// <param name="amountBMin"></param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static bool AddLiquidity(byte[] sender, byte[] to, byte[] tokenA, byte[] tokenB, BigInteger tokenADesired, BigInteger tokenBDesired, BigInteger amountAMin, BigInteger amountBMin, BigInteger deadLine)
        {
            //address tokenA,
            //    address tokenB,
            //uint amountADesired,
            //uint amountBDesired,
            //uint amountAMin,
            //uint amountBMin,
            //    address to,
            //uint deadline
            //验证权限
            if (!Runtime.CheckWitness(sender))
                throw new InvalidOperationException("Forbidden");
            //看看有没有超过最后期限
            BigInteger execBlock = Blockchain.GetHeight();
            if (execBlock > deadLine)
                throw new InvalidOperationException("Exceeded the deadline");
            //现在已经发行的uni的数量

            var totalA = GetReserveToken(tokenA);
            var totalB = GetReserveToken(tokenB);
            BigInteger amountA = 0;
            BigInteger amountB = 0;
            if (totalA == 0 && totalB == 0)
            {
                amountA = tokenADesired;
                amountB = tokenBDesired;
            }
            else
            {
                var estimatedB = Quote(amountA, totalA, totalB);
                if (estimatedB <= tokenBDesired)
                {
                    if (estimatedB >= amountBMin) throw new InvalidOperationException("Insufficient B Amount");
                    amountA = tokenADesired;
                    amountB = estimatedB;
                }
                else
                {
                    var estimatedA = Quote(amountB, totalB, totalA);
                    if (estimatedA >= amountAMin) throw new InvalidOperationException("Insufficient A Amount");
                    amountA = estimatedA;
                    amountB = tokenBDesired;
                }
            }

            var tranResultA = DynamicTransfer(tokenA, sender, to, amountA);
            var tranResultB = DynamicTransfer(tokenB, sender, to, amountB);
            if (!tranResultA || !tranResultB)
            {
                throw new InvalidOperationException("Transfer token error");
            }


            return true;
        }



        /// <summary>
        /// 恒定积报价
        /// </summary>
        /// <param name="amountA"></param>
        /// <param name="totalA"></param>
        /// <param name="totalB"></param>
        private static BigInteger Quote(BigInteger amountA, BigInteger totalA, BigInteger totalB)
        {
            if (amountA <= 0) throw new ArgumentException("[Quote Error] Amount A Invalid");
            if (totalA <= 0) throw new ArgumentException("[Quote Error] Total A Invalid");
            if (totalB <= 0) throw new ArgumentException("[Quote Error] Total B Invalid");
            var amountB = amountA * totalB / totalA;
            return amountB;
        }

        private static BigInteger GetReserveToken(byte[] token)
        {
            if (token == Token0) return GetReserve0();
            if (token == Token1) return GetReserve1();
            throw new Exception("token invalid");
        }
    }
}
