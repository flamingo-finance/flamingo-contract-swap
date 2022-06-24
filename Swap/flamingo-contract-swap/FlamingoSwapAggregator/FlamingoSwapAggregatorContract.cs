﻿using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;

namespace FlamingoSwapAggregator
{
    [DisplayName("Flamingo Aggregator")]
    [ManifestExtra("Author", "Flamingo Finance")]
    [ManifestExtra("Email", "developer@flamingo.finance")]
    [ManifestExtra("Description", "This is a Flamingo Contract")]
    [ContractPermission("*")]//avoid native contract hash change
    public partial class FlamingoSwapAggregatorContract : SmartContract
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
                var estimatedB = GetAMMQuote(amountADesired, reserveA, reserveB);
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
                    var estimatedA = GetAMMQuote(amountBDesired, reserveB, reserveA);
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
                var estimatedB = GetAMMQuote(amountADesired, reserveA, reserveB);
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
                    var estimatedA = GetAMMQuote(amountBDesired, reserveB, reserveA);
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

            var tokenAIsToken0 = tokenA.ToUInteger() < tokenB.ToUInteger();
            var amountA = tokenAIsToken0 ? amounts[0] : amounts[1];
            var amountB = tokenAIsToken0 ? amounts[1] : amounts[0];

            Assert(amountA >= amountAMin, "Insufficient A Amount");
            Assert(amountB >= amountBMin, "Insufficient B Amount");

            return new BigInteger[] { amountA, amountB };
        }


        /// <summary>
        /// 根据资金池输入A获取兑换B的量（等值报价）
        /// </summary>
        /// <param name="amountA">tokenA的输入量</param>
        /// <param name="reserveA">tokenA的总量</param>
        /// <param name="reserveB">tokenB的总量</param>
        /// <returns></returns>
        public static BigInteger GetAMMQuote(BigInteger amountA, BigInteger reserveA, BigInteger reserveB)
        {
            Assert(amountA > 0 && reserveA > 0 && reserveB > 0, "Amount|Reserve Invalid", amountA, reserveA, reserveB);
            var amountB = amountA * reserveB / reserveA;
            return amountB;
        }


        /// <summary>
        /// 同时使用资金池和限价簿，计算可兑换的输出，以及输入的分配策略
        /// </summary>
        /// <param name="amountIn"></param>
        /// <param name="tokenIn"></param>
        /// <param name="tokenOut"></param>
        /// <returns></returns>
        public static BigInteger GetAmountOut(BigInteger amountIn, UInt160 tokenIn, UInt160 tokenOut)
        {
            var strategy = GetStrategyOut(amountIn, tokenIn, tokenOut);
            return strategy[2] + strategy[3];
        }

        private static BigInteger[] GetStrategyOut(BigInteger amountIn, UInt160 tokenIn, UInt160 tokenOut)
        {
            var isBuy = tokenOut == GetBaseToken(tokenIn, tokenOut);
            var ammReverse = GetReserves(tokenIn, tokenOut);
            var leftIn = amountIn;

            BigInteger totalToBook = 0;
            BigInteger totalToPool = 0;
            BigInteger totalOutBook = 0;
            BigInteger totalOutPool = 0;
            BigInteger lastDealPrice = 0;

            if (BookTradable(tokenIn, tokenOut))
            {
                var quoteScale = GetQuoteScale(tokenIn, tokenOut);
                (var anchorID, var bookPrice) = GetOrderBookPrice(tokenIn, tokenOut, isBuy);

                while (bookPrice > 0)
                {
                    var ammPrice = isBuy ? GetAMMPrice(ammReverse[1], ammReverse[0], quoteScale) : GetAMMPrice(ammReverse[0], ammReverse[1], quoteScale);

                    // First AMM
                    if ((isBuy && PriceAddAMMFee(ammPrice) < PriceAddBookFee(bookPrice)) || (!isBuy && PriceAddAMMFee(ammPrice) > PriceAddBookFee(bookPrice)))
                    {
                        var amountToPool = GetAMMAmountInTillPrice(isBuy, PriceRemoveAMMFee(PriceAddBookFee(bookPrice)), quoteScale, ammReverse[0], ammReverse[1]);
                        if (leftIn <= amountToPool) amountToPool = leftIn;
                        var amountOutPool = GetAMMAmountOut(amountToPool, ammReverse[0], ammReverse[1]);
                        totalToPool += amountToPool;
                        totalOutPool += amountOutPool;
                        ammReverse[0] += amountToPool;
                        ammReverse[1] -= amountOutPool;
                        leftIn -= amountToPool;
                    }

                    if (leftIn == 0) break;

                    // Then book
                    var result = GetOrderBookAmountOut(tokenIn, tokenOut, anchorID, bookPrice, leftIn);
                    totalToBook += leftIn - result[0];
                    totalOutBook += result[1];
                    lastDealPrice = bookPrice;
                    leftIn = result[0];

                    if (leftIn == 0) break;
                    (anchorID, bookPrice) = GetOrderBookNextPrice(anchorID);
                }
            }

            // Finally AMM
            if (leftIn > 0)
            {
                totalToPool += leftIn;
                totalOutPool += GetAMMAmountOut(leftIn, ammReverse[0], ammReverse[1]);
            }

            return new BigInteger[] { totalToBook, totalToPool, totalOutBook, totalOutPool, lastDealPrice };
        }


        /// <summary>
        /// 同时使用资金池和限价簿，计算需要提供的输入，以及输入的分配策略
        /// </summary>
        /// <param name="amountOut"></param>
        /// <param name="tokenIn"></param>
        /// <param name="tokenOut"></param>
        /// <returns></returns>
        public static BigInteger GetAmountIn(BigInteger amountOut, UInt160 tokenIn, UInt160 tokenOut)
        {
            var strategy = GetStrategyIn(amountOut, tokenIn, tokenOut);
            return strategy[0] + strategy[1];
        }

        private static BigInteger[] GetStrategyIn(BigInteger amountOut, UInt160 tokenIn, UInt160 tokenOut)
        {
            var isBuy = tokenOut == GetBaseToken(tokenIn, tokenOut);
            var leftOut = amountOut;
            var ammReverse = GetReserves(tokenIn, tokenOut);

            BigInteger totalToBook = 0;
            BigInteger totalToPool = 0;
            BigInteger totalOutBook = 0;
            BigInteger totalOutPool = 0;
            BigInteger lastDealPrice = 0;

            if (BookTradable(tokenIn, tokenOut))
            {
                var quoteScale = GetQuoteScale(tokenIn, tokenOut);
                (var anchorID, var bookPrice) = GetOrderBookPrice(tokenIn, tokenOut, isBuy);

                while (bookPrice > 0)
                {
                    var ammPrice = isBuy ? GetAMMPrice(ammReverse[1], ammReverse[0], quoteScale) : GetAMMPrice(ammReverse[0], ammReverse[1], quoteScale);

                    // First AMM
                    if ((isBuy && PriceAddAMMFee(ammPrice) < PriceAddBookFee(bookPrice)) || (!isBuy && PriceAddAMMFee(ammPrice) > PriceAddBookFee(bookPrice)))
                    {
                        var amountToPool = GetAMMAmountInTillPrice(isBuy, PriceRemoveAMMFee(PriceAddBookFee(bookPrice)), quoteScale, ammReverse[0], ammReverse[1]);
                        var amountOutPool = GetAMMAmountOut(amountToPool, ammReverse[0], ammReverse[1]);
                        if (amountOutPool >= leftOut)
                        {
                            amountToPool = GetAMMAmountIn(leftOut, ammReverse[0], ammReverse[1]);
                            amountOutPool = leftOut;
                        }
                        totalToPool += amountToPool;
                        totalOutPool += amountOutPool;
                        ammReverse[0] += amountToPool;
                        ammReverse[1] -= amountOutPool;
                        leftOut -= amountOutPool;
                    }

                    if (leftOut == 0) break;

                    // Then book
                    var result = GetOrderBookAmountIn(tokenIn, tokenOut, anchorID, bookPrice, leftOut);
                    totalToBook += result[1];
                    totalOutBook += leftOut - result[0];
                    lastDealPrice = bookPrice;
                    leftOut = result[0];

                    if (leftOut == 0) break;
                    (anchorID, bookPrice) = GetOrderBookNextPrice(anchorID);
                }
            }

            // Finally AMM
            if (leftOut > 0)
            {
                totalToPool += GetAMMAmountIn(leftOut, ammReverse[0], ammReverse[1]);
                totalOutPool += leftOut;
            }

            return new BigInteger[] { totalToBook, totalToPool, totalOutBook, totalOutPool, lastDealPrice };
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
            Assert(amountIn > 0 && reserveIn > 0 && reserveOut > 0, "AmountIn Must > 0");

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
            Assert(amountOut > 0 && reserveIn > 0 && reserveOut > 0, "AmountOut Must > 0");
            var numerator = reserveIn * amountOut * 1000;
            var denominator = (reserveOut - amountOut) * 997;
            var amountIn = (numerator / denominator) + 1;
            return amountIn;
        }


        /// <summary>
        /// 根据资金池库存计算对应报价
        /// </summary>
        /// <param name="reverseBase">基准库存</param>
        /// <param name="reverseQuote">报价库存</param>
        /// <param name="quoteScale">报价精度</param>
        /// <returns></returns>
        public static BigInteger GetAMMPrice(BigInteger reverseBase, BigInteger reverseQuote, BigInteger quoteScale)
        {
            Assert(reverseBase > 0 && reverseQuote > 0, "Reserve Invalid");
            return reverseQuote * quoteScale / reverseBase;
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
            var reverseInNew = BigInteger.Pow(reserveIn, 2) * 9 / 1000000;
            if (isBuy) reverseInNew += reserveIn * reserveOut * price * 3988 / quoteScale / 1000;
            else reverseInNew += reserveIn * reserveOut * quoteScale * 3988 / price / 1000;
            reverseInNew = (reverseInNew.Sqrt() - reserveIn * 3 / 1000) * 1000 / 1994;
            return reverseInNew - reserveIn;
        }


        /// <summary>
        /// 获取链式交易报价和交易策略
        /// </summary>
        /// <param name="amountIn">第一种token输入量</param>
        /// <param name="paths">兑换链Token列表(正向：tokenIn,token1,token2...,tokenOut)</param>
        /// <returns></returns>
        public static BigInteger GetAmountOut(BigInteger amountIn, UInt160[] paths)
        {
            var strategies = GetStrategiesOut(amountIn, paths);
            return strategies[strategies.Count - 1][2] + strategies[strategies.Count - 1][3];
        }

        private static List<BigInteger[]> GetStrategiesOut(BigInteger amountIn, UInt160[] paths)
        {
            Assert(paths.Length >= 2, "INVALID_PATH");
            var amounts = new List<BigInteger[]>();

            for (var i = 0; i < paths.Length - 1; i++)
            {
                amounts.Add(GetStrategyOut(amountIn, paths[i], paths[i + 1]));
                amountIn = amounts[i][2] + amounts[i][3];
            }
            return amounts;
        }


        /// <summary>
        /// 获取链式交易逆向报价和交易策略
        /// </summary>
        /// <param name="amountOut">最后一种token输出量</param>
        /// <param name="paths">兑换链Token列表(正向：tokenIn,token1,token2...,tokenOut)</param>
        /// <returns></returns>
        public static BigInteger GetAmountIn(BigInteger amountOut, UInt160[] paths)
        {
            var strategies = GetStrategiesIn(amountOut, paths);
            return strategies[strategies.Count - 1][0] + strategies[strategies.Count - 1][1];
        }

        private static List<BigInteger[]> GetStrategiesIn(BigInteger amountOut, UInt160[] paths)
        {
            Assert(paths.Length >= 2, "INVALID_PATH");
            var amounts = new List<BigInteger[]>();

            for (var i = 0; i < paths.Length - 1; i++)
            {
                amounts.Add(GetStrategyIn(amountOut, paths[paths.Length - i - 2], paths[paths.Length - i - 1]));
                amountOut = amounts[i][0] + amounts[i][1];
            }
            return amounts;
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

            var amounts = GetStrategiesOut(amountIn, paths);
            var amountOut = amounts[amounts.Count - 1][2] + amounts[amounts.Count - 1][3];
            Assert(amountOut >= amountOutMin, "Insufficient AmountOut");

            var me = Runtime.ExecutingScriptHash;
            SafeTransfer(paths[0], sender, me, amountIn);
            for (int i = 0; i < paths.Length - 1; i++)
            {
                SwapWithOrderBook(paths[i], paths[i + 1], amounts[i][0], amounts[i][1], amounts[i][2], amounts[i][3], amounts[i][4]);
            }
            SafeTransfer(paths[paths.Length - 1], me, sender, amountOut);
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

            var amounts = GetStrategiesOut(amountIn, paths);
            var amountOut = amounts[amounts.Count - 1][2] + amounts[amounts.Count - 1][3];
            Assert(amountOut >= amountOutMin, "Insufficient AmountOut");

            var me = Runtime.ExecutingScriptHash;
            RequestTransfer(paths[0], caller, me, amountIn);
            for (int i = 0; i < paths.Length - 1; i++)
            {
                SwapWithOrderBook(paths[i], paths[i + 1], amounts[i][0], amounts[i][1], amounts[i][2], amounts[i][3], amounts[i][4]);
            }
            SafeTransfer(paths[paths.Length - 1], me, caller, amountOut);
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

            var amounts = GetStrategiesIn(amountOut, paths);
            var amountIn = amounts[amounts.Count - 1][0] + amounts[amounts.Count - 1][1];
            Assert(amountIn <= amountInMax, "Excessive AmountIn");

            var me = Runtime.ExecutingScriptHash;
            SafeTransfer(paths[0], sender, me, amountIn);
            for (int i = 0; i < paths.Length - 1; i++)
            {
                var index = paths.Length - 2 - i;
                SwapWithOrderBook(paths[i], paths[i + 1], amounts[index][0], amounts[index][1], amounts[index][2], amounts[index][3], amounts[index][4]);
            }
            SafeTransfer(paths[paths.Length - 1], me, sender, amountOut);
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

            var amounts = GetStrategiesIn(amountOut, paths);
            var amountIn = amounts[amounts.Count - 1][0] + amounts[amounts.Count - 1][1];
            Assert(amountIn <= amountInMax, "Excessive AmountIn");

            var me = Runtime.ExecutingScriptHash;
            RequestTransfer(paths[0], caller, me, amountIn);
            for (int i = 0; i < paths.Length - 1; i++)
            {
                var index = paths.Length - 2 - i;
                SwapWithOrderBook(paths[i], paths[i + 1], amounts[index][0], amounts[index][1], amounts[index][2], amounts[index][3], amounts[index][4]);
            }
            SafeTransfer(paths[paths.Length - 1], me, caller, amountOut);
            return true;
        }


        /// <summary>
        /// 根据计算好的输入和输出，同时使用资金池和限价簿完成兑换
        /// </summary>
        /// <param name="tokenIn"></param>
        /// <param name="tokenOut"></param>
        /// <param name="amountToBook"></param>
        /// <param name="amountToPool"></param>
        /// <param name="amountOutBook"></param>
        /// <param name="amountOutPool"></param>
        /// <param name="bookDealPrice"></param>
        /// <returns></returns>
        private static void SwapWithOrderBook(UInt160 tokenIn, UInt160 tokenOut, BigInteger amountToBook, BigInteger amountToPool, BigInteger amountOutBook, BigInteger amountOutPool, BigInteger bookDealPrice)
        {
            var me = Runtime.ExecutingScriptHash;
            var isBuy = tokenOut == GetBaseToken(tokenIn, tokenOut);

            if (amountToBook > 0)
            {
                Approve(tokenIn, OrderBook, amountToBook);
                if (isBuy)
                {
                    Assert(SendMarketOrder(tokenIn, tokenOut, isBuy, bookDealPrice, (amountOutBook * 10000 + 9984) / 9985) == 0, "Not Full-filled");
                }
                else
                {
                    Assert(SendMarketOrder(tokenIn, tokenOut, isBuy, bookDealPrice, amountToBook) == 0, "Not Full-filled");
                }
                Retrieve(tokenIn, OrderBook);
            }

            if (amountToPool > 0)
            {
                SwapAMM(me, tokenIn, tokenOut, amountToPool, amountOutPool);
            }
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
