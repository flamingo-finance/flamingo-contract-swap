using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;

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
        /// 同时使用资金池和限价簿，计算可兑换的输出
        /// </summary>
        /// <param name="amountInMax"></param>
        /// <param name="tokenIn"></param>
        /// <param name="tokenOut"></param>
        /// <returns></returns>
        public static BigInteger GetAmountOut(BigInteger amountInMax, UInt160 tokenIn, UInt160 tokenOut)
        {
            var isBuy = tokenOut == GetBaseToken(tokenIn, tokenOut);

            var quoteDecimals = GetQuoteDecimals(tokenIn, tokenOut);
            var bookPrice = GetOrderBookPrice(tokenIn, tokenOut, isBuy);
            var ammReverse = GetReserves(tokenIn, tokenOut);

            var leftIn = amountInMax;
            BigInteger totalOut = 0;
            while (bookPrice > 0 && leftIn > 0)
            {
                var ammPrice = isBuy ? GetAMMPrice(ammReverse[1], ammReverse[0], quoteDecimals) : GetAMMPrice(ammReverse[0], ammReverse[1], quoteDecimals);

                // First AMM
                if ((isBuy && PriceAddAMMFee(ammPrice) < PriceAddBookFee(bookPrice)) || (!isBuy && PriceAddAMMFee(ammPrice) > PriceAddBookFee(bookPrice)))
                {
                    var amountToPool = GetAMMAmountInTillPrice(isBuy, PriceRemoveAMMFee(PriceAddBookFee(bookPrice)), quoteDecimals, ammReverse[0], ammReverse[1]);
                    if (leftIn <= amountToPool)
                    {
                        var amountOut = GetAMMAmountOut(leftIn, ammReverse[0], ammReverse[1]);
                        leftIn = 0;
                        totalOut += amountOut;
                        break;
                    }
                    else
                    {
                        var amountOut = GetAMMAmountOut(amountToPool, ammReverse[0], ammReverse[1]);
                        leftIn -= amountToPool;
                        totalOut += amountOut;
                        ammReverse[0] += amountToPool;
                        ammReverse[1] -= amountOut;
                    }
                }

                // Then book
                var result = GetOrderBookAmountOut(tokenIn, tokenOut, bookPrice, bookPrice, leftIn);
                leftIn = result[0];
                totalOut += result[1];
                bookPrice = GetOrderBookNextPrice(tokenIn, tokenOut, isBuy, bookPrice);
            }

            // Finally AMM
            if (leftIn > 0)
            {
                totalOut += GetAMMAmountOut(leftIn, ammReverse[0], ammReverse[1]);
            }

            return totalOut;
        }


        /// <summary>
        /// 同时使用资金池和限价簿，计算需要提供的输入
        /// </summary>
        /// <param name="amountOutMin"></param>
        /// <param name="tokenIn"></param>
        /// <param name="tokenOut"></param>
        /// <returns></returns>
        public static BigInteger GetAmountIn(BigInteger amountOutMin, UInt160 tokenIn, UInt160 tokenOut)
        {
            var isBuy = tokenOut == GetBaseToken(tokenIn, tokenOut);

            var quoteDecimals = GetQuoteDecimals(tokenIn, tokenOut);
            var bookPrice = GetOrderBookPrice(tokenIn, tokenOut, isBuy);
            var ammReverse = GetReserves(tokenIn, tokenOut);

            var leftOut = amountOutMin;
            BigInteger totalIn = 0;
            while (bookPrice > 0 && leftOut > 0)
            {
                var ammPrice = isBuy ? GetAMMPrice(ammReverse[1], ammReverse[0], quoteDecimals) : GetAMMPrice(ammReverse[0], ammReverse[1], quoteDecimals);

                // First AMM
                if ((isBuy && PriceAddAMMFee(ammPrice) < PriceAddBookFee(bookPrice)) || (!isBuy && PriceAddAMMFee(ammPrice) > PriceAddBookFee(bookPrice)))
                {
                    var amountToPool = GetAMMAmountInTillPrice(isBuy, PriceRemoveAMMFee(PriceAddBookFee(bookPrice)), quoteDecimals, ammReverse[0], ammReverse[1]);
                    var amountOutPool = GetAMMAmountOut(amountToPool, ammReverse[0], ammReverse[1]);
                    if (amountOutPool >= leftOut)
                    {
                        var amountIn = GetAMMAmountIn(leftOut, ammReverse[0], ammReverse[1]);
                        leftOut = 0;
                        totalIn += amountIn;
                        break;
                    }
                    else
                    {
                        leftOut -= amountOutPool;
                        totalIn += amountToPool;
                        ammReverse[0] += amountToPool;
                        ammReverse[1] -= amountOutPool;
                    }
                }

                // Then book
                var result = GetOrderBookAmountIn(tokenIn, tokenOut, bookPrice, bookPrice, leftOut);
                leftOut = result[0];
                totalIn += result[1];
                bookPrice = GetOrderBookNextPrice(tokenIn, tokenOut, isBuy, bookPrice);
            }

            // Finally AMM
            if (leftOut > 0)
            {
                totalIn += GetAMMAmountIn(leftOut, ammReverse[0], ammReverse[1]);
            }
            return totalIn;
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
        /// <param name="quoteDecimals">报价精度</param>
        /// <returns></returns>
        public static BigInteger GetAMMPrice(BigInteger reverseBase, BigInteger reverseQuote, int quoteDecimals)
        {
            Assert(reverseBase > 0 && reverseQuote > 0, "Reserve Invalid");
            return reverseQuote * BigInteger.Pow(10, quoteDecimals) / reverseBase;
        }


        /// <summary>
        /// 根据要达到的限价簿价格，计算资金池需要输入的Token量
        /// </summary>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="quoteDecimals"></param>
        /// <param name="reserveIn"></param>
        /// <param name="reserveOut"></param>
        public static BigInteger GetAMMAmountInTillPrice(bool isBuy, BigInteger price, int quoteDecimals, BigInteger reserveIn, BigInteger reserveOut)
        {
            Assert(price > 0 && quoteDecimals > 0 && reserveIn > 0 && reserveOut > 0, "Parameter Invalid");
            var reverseInNew = BigInteger.Pow(reserveIn, 2) * 9 / 1000000;
            if (isBuy) reverseInNew += reserveIn * reserveOut * price * 3988 / BigInteger.Pow(10, quoteDecimals) / 1000;
            else reverseInNew += reserveIn * reserveOut * BigInteger.Pow(10, quoteDecimals) * 3988 / price / 1000;
            reverseInNew = (reverseInNew.Sqrt() - reserveIn * 3 / 1000) * 1000 / 1994;
            return reverseInNew - reserveIn;
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
                amounts[nextIndex] = GetAmountOut(amounts[i], paths[i], paths[nextIndex]);
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
                amounts[preIndex] = GetAmountIn(amounts[i], paths[preIndex], paths[i]);
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
            //验证权限
            Assert(Runtime.CheckWitness(sender), "Forbidden");
            //看看有没有超过最后期限
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded the deadline");

            var amounts = GetAmountsOut(amountIn, paths);
            Assert(amounts[amounts.Length - 1] >= amountOutMin, "Insufficient AmountOut");

            var me = Runtime.ExecutingScriptHash;
            SafeTransfer(paths[0], sender, me, amounts[0]);
            for (int i = 0; i < paths.Length - 1; i++)
            {
                SwapWithOrderBook(paths[i], paths[i + 1], amounts[i], amounts[i + 1]);
            }
            SafeTransfer(paths[paths.Length - 1], me, sender, amounts[amounts.Length - 1]);
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
            //验证权限
            Assert(Runtime.CheckWitness(sender), "Forbidden");
            //看看有没有超过最后期限
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded the deadline");

            var amounts = GetAmountsIn(amountOut, paths);
            Assert(amounts[0] <= amountInMax, "Excessive AmountIn");

            var me = Runtime.ExecutingScriptHash;
            SafeTransfer(paths[0], sender, me, amounts[0]);
            for (int i = 0; i < paths.Length - 1; i++)
            {
                SwapWithOrderBook(paths[i], paths[i + 1], amounts[i], amounts[i + 1]);
            }
            SafeTransfer(paths[paths.Length - 1], me, sender, amounts[amounts.Length - 1]);
            return true;
        }

        /// <summary>
        /// 根据计算好的输入和输出，router同时使用资金池和限价簿完成兑换
        /// </summary>
        /// <param name="tokenIn"></param>
        /// <param name="tokenOut"></param>
        /// <param name="amountIn"></param>
        /// <param name="amountOut"></param>
        /// <returns></returns>
        private static void SwapWithOrderBook(UInt160 tokenIn, UInt160 tokenOut, BigInteger amountIn, BigInteger amountOut)
        {
            var isBuy = tokenOut == GetBaseToken(tokenIn, tokenOut);
            var quoteDecimals = GetQuoteDecimals(tokenIn, tokenOut);
            var me = Runtime.ExecutingScriptHash;

            var leftIn = amountIn;
            BigInteger totalOut = 0;
            while (leftIn > 0)
            {
                var bookPrice = GetOrderBookPrice(tokenIn, tokenOut, isBuy);
                if (bookPrice == 0) break;

                var ammReverse = GetReserves(tokenIn, tokenOut);
                var ammPrice = isBuy ? GetAMMPrice(ammReverse[1], ammReverse[0], quoteDecimals) : GetAMMPrice(ammReverse[0], ammReverse[1], quoteDecimals);

                // First AMM
                if ((isBuy && PriceAddAMMFee(ammPrice) < PriceAddBookFee(bookPrice)) || (!isBuy && PriceAddAMMFee(ammPrice) > PriceAddBookFee(bookPrice)))
                {
                    var amountToPool = GetAMMAmountInTillPrice(isBuy, PriceRemoveAMMFee(PriceAddBookFee(bookPrice)), quoteDecimals, ammReverse[0], ammReverse[1]);
                    if (leftIn <= amountToPool)
                    {
                        var amountOutPool = GetAMMAmountOut(leftIn, ammReverse[0], ammReverse[1]);
                        SwapAMM(me, tokenIn, tokenOut, leftIn, amountOutPool);
                        leftIn = 0;
                        totalOut += amountOutPool;
                        break;
                    }
                    else
                    {
                        var amountOutPool = GetAMMAmountOut(amountToPool, ammReverse[0], ammReverse[1]);
                        SwapAMM(me, tokenIn, tokenOut, amountToPool, amountOutPool);
                        leftIn -= amountToPool;
                        totalOut += amountOutPool;
                    }
                }

                // Then book
                var result = GetOrderBookAmountOut(tokenIn, tokenOut, bookPrice, bookPrice, leftIn);
                var amountToBook = leftIn - result[0];
                var amountOutBook = result[1];

                Approve(tokenIn, OrderBook, amountToBook);
                if (isBuy) Assert(SendMarketOrder(tokenIn, tokenOut, me, isBuy, bookPrice, (amountOutBook * 10000 + 9984) / 9985) == 0, "Not Full-filled");
                else Assert(SendMarketOrder(tokenIn, tokenOut, me, isBuy, bookPrice, amountToBook) == 0, "Not Full-filled");
                Retrieve(tokenIn, OrderBook);
                leftIn = result[0];
                totalOut += amountOutBook;
            }

            // Finally AMM
            if (leftIn > 0)
            {
                var ammReverse = GetReserves(tokenIn, tokenOut);
                var amountOutPool = GetAMMAmountOut(leftIn, ammReverse[0], ammReverse[1]);
                SwapAMM(me, tokenIn, tokenOut, leftIn, amountOutPool);
                totalOut += amountOutPool;
            }
            Assert(totalOut >= amountOut, "AmountOut Not Expected");
        }


        /// <summary>
        /// 向资金池兑换直到QuoteToken存量和BaseToken存量的比值等于指定的限价簿价格
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="amountInMax"></param>
        /// <param name="amountOutMin"></param>
        /// <param name="tokenIn"></param>
        /// <param name="tokenOut"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="quoteDecimals"></param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static bool SwapAMMTillPrice(UInt160 sender, BigInteger amountInMax, BigInteger amountOutMin, UInt160 tokenIn, UInt160 tokenOut, bool isBuy, BigInteger price, int quoteDecimals, BigInteger deadLine)
        {
            Assert(Runtime.CheckWitness(sender), "Forbidden");
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded the deadline");

            var data = GetReserves(tokenIn, tokenOut);

            var amountIn = GetAMMAmountInTillPrice(isBuy, price, quoteDecimals, data[0], data[1]);
            var amountOut = GetAMMAmountOut(amountIn, data[0], data[1]);

            Assert(amountOut >= amountOutMin, "Insufficient AmountOut", amountOut);
            Assert(amountIn <= amountInMax, "Excessive AmountIn", amountIn);

            SwapAMM(sender, tokenIn, tokenOut, amountIn, amountOut);
            return true;
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
