using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract;

namespace FlamingoSwapRouter
{
    [DisplayName("Flamingo Router")]
    [ManifestExtra("Author", "Flamingo Finance")]
    [ManifestExtra("Email", "developer@flamingo.finance")]
    [ManifestExtra("Description", "This is a Flamingo Contract")]
    [ContractPermission("*")]//avoid native contract hash change
    public partial class FlamingoSwapRouterContract : SmartContract
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender">流动性提供者，用于校验签名，以及接收收益</param>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="amountADesired">期望最多转入A的量，The amount of tokenA to add as liquidity if the B/A price is <= amountBDesired/amountADesired (A depreciates).</param>
        /// <param name="amountBDesired">期望最多转入B的量</param>
        /// <param name="amountAMin">预计最少转入A的量，Bounds the extent to which the B/A price can go up before the transaction reverts. Must be <= amountADesired.</param>
        /// <param name="amountBMin">预计最少转入B的量</param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static BigInteger[] AddLiquidity(UInt160 sender, UInt160 tokenA, UInt160 tokenB, BigInteger amountADesired, BigInteger amountBDesired, BigInteger amountAMin, BigInteger amountBMin, BigInteger deadLine)
        {
            //验证参数
            Assert(sender.IsValid && tokenA.IsValid && tokenB.IsValid && amountADesired >= 0 && amountBDesired >= 0 && amountAMin >= 0 && amountBMin >= 0 && deadLine > 0, "Invalid Parameters");
            //验证权限
            Assert(Runtime.CheckWitness(sender), "Forbidden");
            //看看有没有超过最后期限
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded the deadline");

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
                    Assert(estimatedB >= amountBMin, "Insufficient B Amount");
                    amountA = amountADesired;
                    amountB = estimatedB;
                }
                else
                {
                    //B超出期望最大值，按照 TokenB 期望最大值计算 TokenA 的注入量
                    var estimatedA = Quote(amountBDesired, reserveB, reserveA);
                    Assert(estimatedA <= amountADesired, "Excess A Amount");
                    Assert(estimatedA >= amountAMin, "Insufficient A Amount");
                    amountA = estimatedA;
                    amountB = amountBDesired;
                }
            }
            var pairContract = GetExchangePairWithAssert(tokenA, tokenB);

            SafeTransfer(tokenA, sender, pairContract, amountA);
            SafeTransfer(tokenB, sender, pairContract, amountB);
            var liquidity = pairContract.DynamicMint(sender);
            return new BigInteger[] { amountA, amountB, liquidity };
        }

        public static BigInteger[] AddLiquidity(UInt160 tokenA, UInt160 tokenB, BigInteger amountADesired, BigInteger amountBDesired, BigInteger amountAMin, BigInteger amountBMin, BigInteger deadLine)
        {
            //验证参数
            Assert(tokenA.IsValid && tokenB.IsValid && amountADesired >= 0 && amountBDesired >= 0 && amountAMin >= 0 && amountBMin >= 0 && deadLine > 0, "Invalid Parameters");
            //验证权限
            var caller = Runtime.CallingScriptHash;
            Assert(ContractManagement.GetContract(caller) != null, "Forbidden");
            //看看有没有超过最后期限
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded the deadline");

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
                    Assert(estimatedB >= amountBMin, "Insufficient B Amount");
                    amountA = amountADesired;
                    amountB = estimatedB;
                }
                else
                {
                    //B超出期望最大值，按照 TokenB 期望最大值计算 TokenA 的注入量
                    var estimatedA = Quote(amountBDesired, reserveB, reserveA);
                    Assert(estimatedA <= amountADesired, "Excess A Amount");
                    Assert(estimatedA >= amountAMin, "Insufficient A Amount");
                    amountA = estimatedA;
                    amountB = amountBDesired;
                }
            }
            var pairContract = GetExchangePairWithAssert(tokenA, tokenB);

            RequestTransfer(tokenA, caller, pairContract, amountA);
            RequestTransfer(tokenB, caller, pairContract, amountB);
            var liquidity = pairContract.DynamicMint(caller);
            return new BigInteger[] { amountA, amountB, liquidity };
        }


        /// <summary>
        /// 移除流动性
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="liquidity">移除的liquidity Token量</param>
        /// <param name="amountAMin">tokenA 期望最小提取量,Bounds the extent to which the B/A price can go up before the transaction reverts. Must be <= amountADesired.</param>
        /// <param name="amountBMin">tokenB 期望最小提取量</param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static BigInteger[] RemoveLiquidity(UInt160 sender, UInt160 tokenA, UInt160 tokenB, BigInteger liquidity, BigInteger amountAMin, BigInteger amountBMin, BigInteger deadLine)
        {
            //验证参数
            Assert(sender.IsValid && tokenA.IsValid && tokenB.IsValid && liquidity >= 0 && amountAMin >= 0 && amountBMin >= 0 && deadLine > 0, "Invalid Parameters");
            //验证权限
            Assert(Runtime.CheckWitness(sender), "Forbidden");
            //看看有没有超过最后期限
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded the deadline");

            var pairContract = GetExchangePairWithAssert(tokenA, tokenB);
            SafeTransfer(pairContract, sender, pairContract, liquidity);

            var amounts = pairContract.DynamicBurn(sender);
            //var amounts = (byte[])Contract.Call(pairContract, "burn", CallFlags.All, sender);
            var tokenAIsToken0 = tokenA.ToUInteger() < tokenB.ToUInteger();
            var amountA = tokenAIsToken0 ? amounts[0] : amounts[1];
            var amountB = tokenAIsToken0 ? amounts[1] : amounts[0];

            Assert(amountA >= amountAMin, "Insufficient A Amount");
            Assert(amountB >= amountBMin, "Insufficient B Amount");

            return new BigInteger[] { amountA, amountB };
        }

        public static BigInteger[] RemoveLiquidity(UInt160 tokenA, UInt160 tokenB, BigInteger liquidity, BigInteger amountAMin, BigInteger amountBMin, BigInteger deadLine)
        {
            //验证参数
            Assert(tokenA.IsValid && tokenB.IsValid && liquidity > 0 && amountAMin >= 0 && amountBMin >= 0 && deadLine > 0, "Invalid Parameters");
            //验证权限
            var caller = Runtime.CallingScriptHash;
            Assert(ContractManagement.GetContract(caller) != null, "Forbidden");
            //看看有没有超过最后期限
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded the deadline");

            var me = Runtime.ExecutingScriptHash;
            var pairContract = GetExchangePairWithAssert(tokenA, tokenB);
            RequestTransfer(pairContract, caller, me, liquidity);
            SafeTransfer(pairContract, me, pairContract, liquidity);

            var amounts = pairContract.DynamicBurn(caller);
            //var amounts = (byte[])Contract.Call(pairContract, "burn", CallFlags.All, sender);
            var tokenAIsToken0 = tokenA.ToUInteger() < tokenB.ToUInteger();
            var amountA = tokenAIsToken0 ? amounts[0] : amounts[1];
            var amountB = tokenAIsToken0 ? amounts[1] : amounts[0];

            Assert(amountA >= amountAMin, "Insufficient A Amount");
            Assert(amountB >= amountBMin, "Insufficient B Amount");

            return new BigInteger[] { amountA, amountB };
        }


        /// <summary>
        /// 根据输入A获取兑换B的量（等值报价）
        /// </summary>
        /// <param name="amountA">tokenA的输入量</param>
        /// <param name="reserveA">tokenA的总量</param>
        /// <param name="reserveB">tokenB的总量</param>
        public static BigInteger Quote(BigInteger amountA, BigInteger reserveA, BigInteger reserveB)
        {
            Assert(amountA > 0 && reserveA > 0 && reserveB > 0, "Amount|Reserve Invalid", amountA, reserveA, reserveB);
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
            //    Assert(amountIn > 0, "amountIn should be positive number");
            //    Assert(reserveIn > 0 && reserveOut > 0, "reserve should be positive number");
            Assert(amountIn > 0 && reserveIn > 0 && reserveOut > 0, "AmountIn Must > 0");

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
            //Assert(amountOut > 0, "amountOut should be positive number");
            //Assert(reserveIn > 0 && reserveOut > 0, "reserve should be positive number");
            Assert(amountOut > 0 && reserveIn > 0 && reserveOut > 0, "AmountOut Must > 0");
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
        public static BigInteger[] GetAmountsOut(BigInteger amountIn, UInt160[] paths)
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
        public static BigInteger[] GetAmountsIn(BigInteger amountOut, UInt160[] paths)
        {
            Assert(paths.Length >= 2, "INVALID_PATH");
            var amounts = new BigInteger[paths.Length];
            var max = paths.Length - 1;
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
        public static BigInteger[] GetReserves(UInt160 tokenA, UInt160 tokenB)
        {
            Assert(tokenA.IsValid && tokenB.IsValid, "INVALID_TOKEN");
            var reserveData = (ReservesData)Contract.Call(GetExchangePairWithAssert(tokenA, tokenB), "getReserves", CallFlags.ReadOnly, new object[] { });
            return tokenA.ToUInteger() < tokenB.ToUInteger() ? new BigInteger[] { reserveData.Reserve0, reserveData.Reserve1 } : new BigInteger[] { reserveData.Reserve1, reserveData.Reserve0 };
        }


        public static void OnNEP17Payment(UInt160 sender, BigInteger amountIn, object data)
        {
            
        }


        /// <summary>
        /// 根据输入计算输出，并完成兑换
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="amountIn">用户输入的TokenIn的量</param>
        /// <param name="amountOutMin">用户想要兑换的TokenOut的最小量，实际估算结果低于此值时，交易中断</param>
        /// <param name="paths"></param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static bool SwapTokenInForTokenOut(UInt160 sender, BigInteger amountIn, BigInteger amountOutMin, UInt160[] paths, BigInteger deadLine)
        {
            //验证参数
            Assert(sender.IsValid && amountIn > 0 && amountOutMin >= 0 && paths.Length >= 2 && deadLine > 0, "Invalid Parameters");
            //验证权限
            Assert(Runtime.CheckWitness(sender), "Forbidden");
            //看看有没有超过最后期限
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded the deadline");

            var amounts = GetAmountsOut(amountIn, paths);
            Assert(amounts[amounts.Length - 1] >= amountOutMin, "Insufficient AmountOut");

            var pairContract = GetExchangePairWithAssert(paths[0], paths[1]);
            //先将用户的token转入第一个交易对合约
            SafeTransfer(paths[0], sender, pairContract, amounts[0]);
            Swap(amounts, paths, sender);
            return true;
        }

        public static bool SwapTokenInForTokenOut(BigInteger amountIn, BigInteger amountOutMin, UInt160[] paths, BigInteger deadLine)
        {
            //验证参数
            Assert(amountIn > 0 && amountOutMin >= 0 && paths.Length >= 2 && deadLine > 0, "Invalid Parameters");
            //验证权限
            var caller = Runtime.CallingScriptHash;
            Assert(ContractManagement.GetContract(caller) != null, "Forbidden");
            //看看有没有超过最后期限
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded the deadline");

            var amounts = GetAmountsOut(amountIn, paths);
            Assert(amounts[amounts.Length - 1] >= amountOutMin, "Insufficient AmountOut");

            var pairContract = GetExchangePairWithAssert(paths[0], paths[1]);
            //先将用户的token转入第一个交易对合约
            RequestTransfer(paths[0], caller, pairContract, amounts[0]);
            Swap(amounts, paths, caller);
            return true;
        }


        /// <summary>
        /// 根据输出计算输入量，并完成兑换
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="amountOut">用户输入的TokenOut的量</param>
        /// <param name="amountInMax">用户愿意支付的TokenIn的最大量，预估值高于此值时交易中断</param>
        /// <param name="paths"></param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static bool SwapTokenOutForTokenIn(UInt160 sender, BigInteger amountOut, BigInteger amountInMax, UInt160[] paths, BigInteger deadLine)
        {
            //验证参数
            Assert(sender.IsValid && amountOut > 0 && amountInMax >= 0 && paths.Length >= 2 && deadLine > 0, "Invalid Parameters");
            //验证权限
            Assert(Runtime.CheckWitness(sender), "Forbidden");
            //看看有没有超过最后期限
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded the deadline");

            var amounts = GetAmountsIn(amountOut, paths);
            Assert(amounts[0] <= amountInMax, "Excessive AmountIn");

            var pairContract = GetExchangePairWithAssert(paths[0], paths[1]);
            //先将用户的token转入第一个交易对合约
            SafeTransfer(paths[0], sender, pairContract, amounts[0]);
            Swap(amounts, paths, sender);
            return true;
        }

        public static bool SwapTokenOutForTokenIn(BigInteger amountOut, BigInteger amountInMax, UInt160[] paths, BigInteger deadLine)
        {
            //验证参数
            Assert(amountOut > 0 && amountInMax >= 0 && paths.Length >= 2 && deadLine > 0, "Invalid Parameters");
            //验证权限
            var caller = Runtime.CallingScriptHash;
            Assert(ContractManagement.GetContract(caller) != null, "Forbidden");
            //看看有没有超过最后期限
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded the deadline");

            var amounts = GetAmountsIn(amountOut, paths);
            Assert(amounts[0] <= amountInMax, "Excessive AmountIn");

            var pairContract = GetExchangePairWithAssert(paths[0], paths[1]);
            //先将用户的token转入第一个交易对合约
            RequestTransfer(paths[0], caller, pairContract, amounts[0]);
            Swap(amounts, paths, caller);
            return true;
        }

        private static void Swap(BigInteger[] amounts, UInt160[] paths, UInt160 toAddress)
        {
            var max = paths.Length - 1;
            Assert(paths[0] != paths[max], "Invalid Path");
            for (int i = 0; i < max; i++)
            {
                var input = paths[i];
                var output = paths[i + 1];
                var amountOut = amounts[i + 1];//本轮兑换，合约需要转出的token量

                BigInteger amount0Out = 0;
                BigInteger amount1Out = 0;
                //判定要转出的是token0还是token1
                if (input.ToUInteger() < output.ToUInteger())
                {
                    //input是token0，所以要转出的output是token1
                    amount1Out = amountOut;
                }
                else
                {
                    amount0Out = amountOut;
                }

                var to = toAddress;//最后一轮swap的接收地址
                if (i < paths.Length - 2)
                {
                    //兑换链中每轮的接收地址都是下一对token的pair合约
                    to = GetExchangePairWithAssert(output, paths[i + 2]);
                }

                var pairContract = GetExchangePairWithAssert(input, output);
                //从pair[n,n+1]中转出amount[n+1]到pair[n+1,n+2]
                Contract.Call(pairContract, "swap", CallFlags.All, new object[] { amount0Out, amount1Out, to, null });

            }
        }

    }

}
