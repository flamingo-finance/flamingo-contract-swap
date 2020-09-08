using System;
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;

namespace FlamingoSwapRouter
{
    partial class FlamingoSwapRouterContract : SmartContract
    {
        static readonly byte[] superAdmin = "AZaCs7GwthGy9fku2nFXtbrdKBRmrUQoFP".ToScriptHash();

        private static readonly byte[] Factory = "64c8f037fbe1b599e25ed2442d8ebffa251d03c9".HexToBytes();

        public static object Main(string method, object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(superAdmin);
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                var msgSender = ExecutionEngine.CallingScriptHash;//等价以太坊的msg.sender

                if (method == "quote") return Quote((BigInteger)args[0], (BigInteger)args[1], (BigInteger)args[2]);

                if (method == "getReserves") return GetReserves((byte[])args[0], (byte[])args[1]);

                if (method == "getAmountOut") return GetAmountOut((BigInteger)args[0], (BigInteger)args[1], (BigInteger)args[2]);

                if (method == "getAmountsOut") return GetAmountsOut(args[0].ToBigInt(), (byte[][])args[1]);

                if (method == "getAmountIn") return GetAmountIn((BigInteger)args[0], (BigInteger)args[1], (BigInteger)args[2]);

                if (method == "getAmountsIn") return GetAmountsIn(args[0].ToBigInt(), (byte[][])args[1]);

                if (method == "swapTokenInForTokenOut") return SwapTokenInForTokenOut((byte[])args[0], (BigInteger)args[1], (BigInteger)args[2], (byte[][])args[3], (BigInteger)args[4]);

                if (method == "swapTokenOutForTokenIn") return SwapTokenOutForTokenIn((byte[])args[0], (BigInteger)args[1], (BigInteger)args[2], (byte[][])args[3], (BigInteger)args[4]);

                if (method == "addLiquidity") return AddLiquidity((byte[])args[0], (byte[])args[1], (byte[])args[2], (byte[])args[3], (BigInteger)args[4], (BigInteger)args[5], (BigInteger)args[6], (BigInteger)args[7], (BigInteger)args[8]);

                if (method == "removeLiquidity") return RemoveLiquidity((byte[])args[0], (byte[])args[1], (byte[])args[2], (byte[])args[3], (BigInteger)args[4], (BigInteger)args[5], (BigInteger)args[6], (BigInteger)args[7]);

            }
            return false;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender">流动性提供者，用于校验签名</param>
        /// <param name="to">Liquidity收益地址</param>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="amountADesired">期望最多转入A的量，The amount of tokenA to add as liquidity if the B/A price is <= amountBDesired/amountADesired (A depreciates).</param>
        /// <param name="amountBDesired">期望最多转入B的量</param>
        /// <param name="amountAMin">预计最少转入A的量，Bounds the extent to which the B/A price can go up before the transaction reverts. Must be <= amountADesired.</param>
        /// <param name="amountBMin">预计最少转入B的量</param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static BigInteger[] AddLiquidity(byte[] sender, byte[] to, byte[] tokenA, byte[] tokenB, BigInteger amountADesired, BigInteger amountBDesired, BigInteger amountAMin, BigInteger amountBMin, BigInteger deadLine)
        {
            //验证权限
            Assert(Runtime.CheckWitness(sender), "Forbidden");

            //看看有没有超过最后期限
            BigInteger timestamp = Runtime.Time;
            Assert(timestamp <= deadLine, "Exceeded the deadline");


            var reserves = GetReserves(tokenA, tokenB);
            var reserveA = reserves[0];
            var reserveB = reserves[1];
            BigInteger amountA = 0;
            BigInteger amountB = 0;
            if (reserveA == 0 && reserveB == 0)
            {
                //第一次注入
                amountA = amountADesired;
                amountB = amountBDesired;
            }
            else
            {
                //根据 tokenA 期望最大值预估需要的 tokenB 的注入量
                var estimatedB = Quote(amountADesired, reserveA, reserveB);
                if (estimatedB <= amountBDesired)
                {
                    //B在期望范围内，直接按计算值转
                    Assert(estimatedB >= amountBMin, "Insufficient B Amount", estimatedB);
                    amountA = amountADesired;
                    amountB = estimatedB;
                }
                else
                {
                    //B超出期望最大值，按照 TokenB 期望最大值计算 TokenA 的注入量
                    var estimatedA = Quote(amountBDesired, reserveB, reserveA);
                    Assert(estimatedA >= amountAMin, "Insufficient A Amount", estimatedA);
                    amountA = estimatedA;
                    amountB = amountBDesired;
                }
            }

            var pairContract = GetExchangePairWithAssert(tokenA, tokenB);

            SafeTransfer(tokenA, sender, pairContract, amountA);
            SafeTransfer(tokenB, sender, pairContract, amountB);


            var liquidity = pairContract.DynamicMint(to);
            var result = new BigInteger[3];
            result[0] = amountA;
            result[1] = amountB;
            result[2] = liquidity;
            return result;
        }



        /// <summary>
        /// 移除流动性
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="to"></param>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="liquidity">移除的liquidity Token量</param>
        /// <param name="amountAMin">tokenA 期望最小提取量,Bounds the extent to which the B/A price can go up before the transaction reverts. Must be <= amountADesired.</param>
        /// <param name="amountBMin">tokenB 期望最小提取量</param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static BigInteger[] RemoveLiquidity(byte[] sender, byte[] to, byte[] tokenA, byte[] tokenB, BigInteger liquidity, BigInteger amountAMin, BigInteger amountBMin, BigInteger deadLine)
        {
            //验证权限
            Assert(Runtime.CheckWitness(sender), "Forbidden");
            //看看有没有超过最后期限
            BigInteger timestamp = Runtime.Time;
            Assert(timestamp <= deadLine, "Exceeded the deadline");


            var pairContract = GetExchangePairWithAssert(tokenA, tokenB);
            SafeTransfer(pairContract, sender, pairContract, liquidity);

            var amounts = pairContract.DynamicBurn(to);
            var token0 = tokenA.ToUInteger() < tokenB.ToUInteger() ? tokenA : tokenB;
            var amountA = token0 == tokenA ? amounts[0] : amounts[1];
            var amountB = token0 == tokenA ? amounts[1] : amounts[0];

            Assert(amountA >= amountAMin, "INSUFFICIENT_A_AMOUNT");
            Assert(amountB >= amountBMin, "INSUFFICIENT_B_AMOUNT");
            var result = new BigInteger[2];
            result[0] = amountA;
            result[1] = amountB;
            return result;
        }


        /// <summary>
        /// 根据输入A获取兑换B的量（等值报价）
        /// </summary>
        /// <param name="amountA">tokenA的输入量</param>
        /// <param name="reserveA">tokenA的总量</param>
        /// <param name="reserveB">tokenB的总量</param>
        public static BigInteger Quote(BigInteger amountA, BigInteger reserveA, BigInteger reserveB)
        {
            Assert(amountA > 0, "amountA Invalid");
            Assert(reserveA > 0, "reserveA Invalid");
            Assert(reserveB > 0, "reserveB Invalid");

            var amountB = amountA * reserveB / reserveA;
            return amountB;
        }


        /// <summary>
        /// 根据输入A获取兑换B的量（扣除千分之三手续费）
        /// </summary>
        /// <param name="amountIn"></param>
        /// <param name="reserveIn"></param>
        /// <param name="reserveOut"></param>
        /// <returns></returns>
        public static BigInteger GetAmountOut(BigInteger amountIn, BigInteger reserveIn, BigInteger reserveOut)
        {
            Assert(amountIn > 0, "amountIn should be positive number");
            Assert(reserveIn > 0 && reserveOut > 0, "reserve should be positive number");
            var amountInWithFee = amountIn * 997;
            var numerator = amountInWithFee * reserveOut;
            var denominator = reserveIn * 1000 + amountInWithFee;
            var amountOut = numerator / denominator;
            return amountOut;
        }

        /// <summary>
        /// 根据要兑换的输出量B，计算需要的输入的A实际量（已计算千分之三手续费）
        /// </summary>
        /// <param name="amountOut"></param>
        /// <param name="reserveIn"></param>
        /// <param name="reserveOut"></param>
        /// <returns></returns>
        public static BigInteger GetAmountIn(BigInteger amountOut, BigInteger reserveIn, BigInteger reserveOut)
        {
            Assert(amountOut > 0, "amountOut should be positive number");
            Assert(reserveIn > 0 && reserveOut > 0, "reserve should be positive number");
            var numerator = reserveIn * amountOut * 1000;
            var denominator = (reserveOut - amountOut) * 997;
            var amountIn = (numerator / denominator) + 1;
            return amountIn;
        }


        /// <summary>
        /// 获取链式交易报价
        /// </summary>
        /// <param name="amountIn">第一种token输入量</param>
        /// <param name="paths">兑换链Token列表(正向：tokenIn,token1,token2...,tokenOut）</param>
        /// <returns></returns>
        public static BigInteger[] GetAmountsOut(BigInteger amountIn, byte[][] paths)
        {
            Assert(paths.Length >= 2, "INVALID_PATH");
            var amounts = new BigInteger[paths.Length];
            amounts[0] = amountIn;
            var max = paths.Length - 1;
            for (var i = 0; i < max; i++)
            {
                var nextIndex = i + 1;
                var data = GetReserves(paths[i], paths[nextIndex]);
                amounts[nextIndex] = GetAmountOut(amounts[i], data[0], data[1]);
            }
            return amounts;
        }


        /// <summary>
        /// 获取链式交易逆向报价
        /// </summary>
        /// <param name="amountOut">最后一种token输出量</param>
        /// <param name="paths">兑换链Token列表(正向：tokenIn,token1,token2...,tokenOut）</param>
        /// <returns></returns>
        public static BigInteger[] GetAmountsIn(BigInteger amountOut, byte[][] paths)
        {
            Assert(paths.Length >= 2, "INVALID_PATH");
            var amounts = new BigInteger[paths.Length];
            var max = paths.Length - 1;
            //var a = amountOut.AsByteArray().ToBigInteger();
            amountOut += 0;
            amounts[max] = amountOut;
            for (var i = max; i > 0; i--)
            {
                var preIndex = i - 1;
                var data = GetReserves(paths[preIndex], paths[i]);
                amounts[preIndex] = GetAmountIn(amounts[i], data[0], data[1]);
            }
            return amounts;
        }


        /// <summary>
        /// 查询TokenA,TokenB交易对合约的里的持有量并按A、B顺序返回
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        private static BigInteger[] GetReserves(byte[] tokenA, byte[] tokenB)
        {
            var token0 = tokenA.ToUInteger() < tokenB.ToUInteger() ? tokenA : tokenB;
            var pairContract = GetExchangePairWithAssert(tokenA, tokenB);
            var reserveData = pairContract.DynamicGetReserves();
            var data = new BigInteger[2];
            if (tokenA == token0)
            {
                data[0] = reserveData.Reserve0;
                data[1] = reserveData.Reserve1;
            }
            else
            {
                data[0] = reserveData.Reserve1;
                data[1] = reserveData.Reserve0;
            }
            return data;
        }




        /// <summary>
        /// 根据输入兑换输出
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="amountIn"></param>
        /// <param name="amountOutMin"></param>
        /// <param name="paths"></param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static bool SwapTokenInForTokenOut(byte[] sender, BigInteger amountIn, BigInteger amountOutMin, byte[][] paths, BigInteger deadLine)
        {
            //验证权限
            Assert(Runtime.CheckWitness(sender), "Forbidden");
            //看看有没有超过最后期限
            BigInteger timestamp = Runtime.Time;
            Assert(timestamp <= deadLine, "Exceeded the deadline");

            var amounts = GetAmountsOut(amountIn, paths);
            Assert(amounts[amounts.Length - 1] >= amountOutMin, "INSUFFICIENT_OUTPUT_AMOUNT");

            var pairContract = GetExchangePairWithAssert(paths[0], paths[1]);

            var transfer0 = paths[0].DynamicTransfer(sender, pairContract, amounts[0]);
            Assert(transfer0, "transfer0 fail");

            Swap(amounts, paths, sender);
            return true;
        }


        /// <summary>
        /// 根据输出计算输入量
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="amountOut"></param>
        /// <param name="amountInMax"></param>
        /// <param name="paths"></param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static bool SwapTokenOutForTokenIn(byte[] sender, BigInteger amountOut, BigInteger amountInMax, byte[][] paths, BigInteger deadLine)
        {
            //验证权限
            Assert(Runtime.CheckWitness(sender), "Forbidden");
            //看看有没有超过最后期限
            BigInteger timestamp = Runtime.Time;
            Assert(timestamp <= deadLine, "Exceeded the deadline");


            var amounts = GetAmountsIn(amountOut, paths);
            Assert(amounts[0] <= amountInMax, "EXCESSIVE_INPUT_AMOUNT");

            var pairContract = GetExchangePairWithAssert(paths[0], paths[1]);

            var transfer0 = paths[0].DynamicTransfer(sender, pairContract, amounts[0]);
            Assert(transfer0, "transfer0 fail");
            Swap(amounts, paths, sender);

            return true;
        }

        private static void Swap(BigInteger[] amounts, byte[][] paths, byte[] toAddress)
        {
            var max = paths.Length - 1;
            for (int i = 0; i < max; i++)
            {
                var input = paths[i];
                var output = paths[i + 1];
                var token0 = input.ToUInteger() < output.ToUInteger() ? input : output;
                var amountOut = amounts[i + 1];

                BigInteger amount0Out = 0;
                BigInteger amount1Out = 0;
                if (input == token0)
                {
                    amount1Out = amountOut;
                }
                else
                {
                    amount0Out = amountOut;
                }

                var to = toAddress;
                if (i < paths.Length - 2)
                {
                    to = GetExchangePairWithAssert(output, paths[i + 2]);
                }

                var pairContract = GetExchangePairWithAssert(input, output);
                pairContract.DynamicSwap(amount0Out, amount1Out, to, new byte[0]);
            }
        }




        /// <summary>
        /// 安全查询交易对，查不到立即中断合约执行
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        private static byte[] GetExchangePairWithAssert(byte[] tokenA, byte[] tokenB)
        {
            var pairContract = Factory.DynamicGetExchangePair(tokenA, tokenB);
            Assert(pairContract.Length == 20, "cannot find pairContract");
            return pairContract;
        }


        /// <summary>
        /// 安全转账，失败则中断退出
        /// </summary>
        /// <param name="token"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        private static void SafeTransfer(byte[] token, byte[] from, byte[] to, BigInteger amount)
        {
            var result = token.DynamicTransfer(from, to, amount);
            Assert(result, "Transfer Fail", token);
        }

    }

}
