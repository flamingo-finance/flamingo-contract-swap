using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
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
        public static void CancelOrder(ByteString orderID)
        {
            // Get order and book info
            var index = GetOrderIndex(orderID);
            Assert(index is not null, "Order Not Exists");
            var order = GetOrder(index, orderID);
            Assert(Runtime.CheckWitness(order.Maker), "No Authorization");
            var me = Runtime.ExecutingScriptHash;
            var pairKey = GetPairKey(order.BaseToken, order.QuoteToken);
            var bookInfo = GetBook(pairKey);

            // Remove order and index
            RemoveLimitOrder(pairKey, index, order);
            onOrderStatusChanged(order.BaseToken, order.QuoteToken, orderID, order.IsBuy, order.Maker, order.Price, 0);

            // Do transfer
            if (order.IsBuy) SafeTransfer(order.QuoteToken, me, order.Maker, order.LeftAmount * order.Price / bookInfo.QuoteScale);
            else SafeTransfer(order.BaseToken, me, order.Maker, order.LeftAmount);
        }

        public static BigInteger DealOrder(UInt160 taker, ByteString orderID, BigInteger amount)
        {
            // Check Parameters
            Assert(amount > 0, "Invalid Parameters");
            Assert(Runtime.CheckWitness(taker), "No Authorization");

            // Get order and book info
            var index = GetOrderIndex(orderID);
            Assert(index is not null, "Order Not Exists");
            var order = GetOrder(index, orderID);
            var pairKey = GetPairKey(order.BaseToken, order.QuoteToken);
            var bookInfo = GetBook(pairKey);

            var me = Runtime.ExecutingScriptHash;
            var fundAddress = GetFundAddress();

            var baseAmount = amount > order.LeftAmount ? order.LeftAmount : amount;
            var quoteAmount = baseAmount * order.Price / bookInfo.QuoteScale;
            var basePayment = baseAmount * 997 / 1000;
            var quotePayment = quoteAmount * 997 / 1000;

            if (order.IsBuy) SafeTransfer(order.BaseToken, taker, me, baseAmount);
            else SafeTransfer(order.QuoteToken, taker, me, quoteAmount > 0 ? quoteAmount : 1);

            // Update or remove order
            if (baseAmount < order.LeftAmount)
            {
                order.LeftAmount -= baseAmount;
                UpdateLimitOrder(index, order);
                onOrderStatusChanged(order.BaseToken, order.QuoteToken, orderID, order.IsBuy, order.Maker, order.Price, order.LeftAmount);
            }
            else
            {
                RemoveLimitOrder(pairKey, index, order);
                onOrderStatusChanged(order.BaseToken, order.QuoteToken, orderID, order.IsBuy, order.Maker, order.Price, 0);
            }
            
            // Transfer
            SafeTransfer(order.BaseToken, me, order.IsBuy ? order.Maker : taker, basePayment);
            SafeTransfer(order.QuoteToken, me, order.IsBuy ? taker : order.Maker, quotePayment);

            if (fundAddress is not null)
            {
                SafeTransfer(order.BaseToken, me, fundAddress, baseAmount - basePayment);
                SafeTransfer(order.QuoteToken, me, fundAddress, quoteAmount - quotePayment);
            }

            return amount - baseAmount;
        }

        public static BigInteger[] DealOrders(UInt160 tokenA, UInt160 tokenB, UInt160 taker, bool isBuy, BigInteger amount, ByteString[] orderIDs)
        {
            // Check Parameters
            Assert(amount > 0, "Invalid Parameters");
            Assert(Runtime.CheckWitness(taker), "No Authorization");
            if (orderIDs.Length == 0) return new BigInteger[] { amount, 0 };

            // Get book info
            var pairKey = GetPairKey(tokenA, tokenB);
            var bookInfo = GetBook(pairKey);
            Assert(bookInfo.BaseToken is not null, "Book Not Exists");

            var me = Runtime.ExecutingScriptHash;
            var fundAddress = GetFundAddress();

            var takerPayment = BigInteger.Zero;
            var takerReceive = BigInteger.Zero;
            var baseFee = BigInteger.Zero;
            var quoteFee = BigInteger.Zero;
            var makerReceive = new Map<UInt160, BigInteger>();

            foreach (var id in orderIDs)
            {
                // Get order
                var index = GetOrderIndex(id);
                if (index is null) continue;
                var order = GetOrder(index, id);
                Assert(order.BaseToken == bookInfo.BaseToken && order.QuoteToken == bookInfo.QuoteToken && order.IsBuy ^ isBuy, "Invalid Trading");

                var baseAmount = amount > order.LeftAmount ? order.LeftAmount : amount;
                var quoteAmount = baseAmount * order.Price / bookInfo.QuoteScale;
                var basePayment = baseAmount * 997 / 1000;
                var quotePayment = quoteAmount * 997 / 1000;
                baseFee += baseAmount - basePayment;
                quoteFee += quoteAmount - quotePayment;
                amount -= baseAmount;

                // Record payment
                takerPayment += isBuy ? (quoteAmount > 0 ? quoteAmount : 1) : baseAmount;
                takerReceive += isBuy ? basePayment : quotePayment;
                if (!makerReceive.HasKey(order.Maker)) makerReceive[order.Maker] = 0;
                makerReceive[order.Maker] += isBuy ? quotePayment : basePayment;

                // Update or remove order
                if (baseAmount < order.LeftAmount)
                {
                    order.LeftAmount -= baseAmount;
                    UpdateLimitOrder(index, order);
                    onOrderStatusChanged(order.BaseToken, order.QuoteToken, id, order.IsBuy, order.Maker, order.Price, order.LeftAmount);
                }
                else
                {
                    RemoveLimitOrder(pairKey, index, order);
                    onOrderStatusChanged(order.BaseToken, order.QuoteToken, id, order.IsBuy, order.Maker, order.Price, 0);
                }
            
                if (amount <= 0) break;
            }

            // Do transfer
            SafeTransfer(isBuy ? bookInfo.QuoteToken : bookInfo.BaseToken, taker, me, takerPayment);
            SafeTransfer(isBuy ? bookInfo.BaseToken : bookInfo.QuoteToken, me, taker, takerReceive);
            foreach (var toAddress in makerReceive.Keys) SafeTransfer(isBuy ? bookInfo.QuoteToken : bookInfo.BaseToken, me, toAddress, makerReceive[toAddress]);
            if (fundAddress is not null)
            {
                SafeTransfer(bookInfo.QuoteToken, me, fundAddress, quoteFee);
                SafeTransfer(bookInfo.BaseToken, me, fundAddress, baseFee);
            }

            return new BigInteger[] { amount, takerReceive };
        }

        public static ByteString DealLimitOrder(UInt160 tokenA, UInt160 tokenB, UInt160 maker, bool isBuy, BigInteger amount, BigInteger price, ByteString[] orderIDs)
        {
            // Check Parameters
            Assert(price > 0, "Invalid Parameters");
            Assert(ContractManagement.GetContract(maker) == null, "Forbidden");

            // Orders First
            var result = DealOrders(tokenA, tokenB, maker, isBuy, amount, orderIDs);
            var leftAmount = result[0];

            // Then AMM
            var pairKey = GetPairKey(tokenA, tokenB);
            var bookInfo = GetBook(pairKey);
            Assert(bookInfo.BaseToken is not null, "Book Not Exists");
            var pairContract = GetExchangePairWithAssert(tokenA, tokenB);
            var hasFundFee = HasFundAddress(pairContract);
            var ammReverse = isBuy
                ? GetReserves(pairContract, bookInfo.QuoteToken, bookInfo.BaseToken)
                : GetReserves(pairContract, bookInfo.BaseToken, bookInfo.QuoteToken);

            // Get amountIn and amountOut
            var amountIn = hasFundFee
                ? GetAmountInTillPriceWithFundFee(isBuy, price, bookInfo.QuoteScale, ammReverse[0], ammReverse[1])
                : GetAmountInTillPrice(isBuy, price, bookInfo.QuoteScale, ammReverse[0], ammReverse[1]);
            if (amountIn < 0) amountIn = 0;
            var amountOut = GetAmountOut(amountIn, ammReverse[0], ammReverse[1]);
            if (isBuy && leftAmount < amountOut)
            {
                amountOut = leftAmount;
                amountIn = GetAmountIn(amountOut, ammReverse[0], ammReverse[1]);
            }
            if (!isBuy && leftAmount < amountIn)
            {
                amountIn = leftAmount;
                amountOut = GetAmountOut(amountIn, ammReverse[0], ammReverse[1]);
            }

            // Do swap
            if (amountOut > 0) 
            {
                SwapAMM(pairContract, maker, isBuy ? bookInfo.QuoteToken : bookInfo.BaseToken, isBuy ? bookInfo.BaseToken : bookInfo.QuoteToken, amountIn, amountOut);
                leftAmount -= isBuy ? amountOut : amountIn;
            }

            // Add new limit order
            if (leftAmount < bookInfo.MinOrderAmount || leftAmount > bookInfo.MaxOrderAmount) return null;
            var me = Runtime.ExecutingScriptHash;
            SafeTransfer(isBuy ? bookInfo.QuoteToken : bookInfo.BaseToken, maker, me, isBuy ? leftAmount * price / bookInfo.QuoteScale : leftAmount);
            var id = AddLimitOrder(pairKey, bookInfo.Symbol, new LimitOrder(){
                BaseToken = bookInfo.BaseToken,
                QuoteToken = bookInfo.QuoteToken,
                Time = Runtime.Time,
                IsBuy = isBuy,
                Maker = maker,
                Price = price,
                TotalAmount = amount,
                LeftAmount = leftAmount
            });
            onOrderStatusChanged(bookInfo.BaseToken, bookInfo.QuoteToken, id, isBuy, maker, price, leftAmount);
            return id;
        }

        public static void DealMarketOrder(UInt160 tokenA, UInt160 tokenB, UInt160 taker, bool isBuy, BigInteger amount, BigInteger amountOutMin, ByteString[] orderIDs)
        {
            // Check Parameters
            Assert(amountOutMin > 0, "Invalid Parameters");

            // Orders First
            var result = DealOrders(tokenA, tokenB, taker, isBuy, amount, orderIDs);
            var leftAmount = result[0];
            var receivedPayment = result[1];
            if (leftAmount == 0) return;

            // Then AMM
            var bookInfo = GetBookInfo(tokenA, tokenB);
            Assert(bookInfo.BaseToken is not null, "Book Not Exists");
            var pairContract = GetExchangePairWithAssert(tokenA, tokenB);
            var hasFundFee = HasFundAddress(pairContract);
            var ammReverse = GetReserves(pairContract, bookInfo.BaseToken, bookInfo.QuoteToken);

            // Get amountIn and amountOut
            var amountIn = isBuy ? GetAmountIn(leftAmount, ammReverse[1], ammReverse[0]) : leftAmount;
            var amountOut = isBuy ? leftAmount : GetAmountOut(leftAmount, ammReverse[0], ammReverse[1]);
            Assert(amountOut + receivedPayment >= amountOutMin, "Insufficient AmountOut");

            // Do swap
            SwapAMM(pairContract, taker, isBuy ? bookInfo.QuoteToken : bookInfo.BaseToken, isBuy ? bookInfo.BaseToken : bookInfo.QuoteToken, amountIn, amountOut);
        }

        /// <summary>
        /// Register a new book
        /// </summary>
        /// <param name="baseToken"></param>
        /// <param name="quoteToken"></param>
        /// <param name="quoteDecimals"></param>
        /// <param name="minOrderAmount"></param>
        /// <param name="maxOrderAmount"></param>
        public static void RegisterOrderBook(UInt160 baseToken, UInt160 quoteToken, uint quoteDecimals, BigInteger minOrderAmount, BigInteger maxOrderAmount)
        {
            Assert(baseToken.IsAddress() && quoteToken.IsAddress(), "Invalid Address");
            Assert(baseToken != quoteToken, "Invalid Trade Pair");
            Assert(minOrderAmount > 0 && maxOrderAmount > 0 && minOrderAmount <= maxOrderAmount, "Invalid Amount Limit");
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            var quoteScale = BigInteger.Pow(10, (int)quoteDecimals);
            var bookInfo = GetBook(pairKey);
            Assert(bookInfo.BaseToken is null, "Book Already Exists");

            SetBook(pairKey, new BookInfo(){
                Symbol = GetTokenSymbol(baseToken) + "/" + GetTokenSymbol(quoteToken),
                BaseToken = baseToken,
                QuoteToken = quoteToken,
                QuoteScale = quoteScale,
                MinOrderAmount = minOrderAmount,
                MaxOrderAmount = maxOrderAmount,
                IsPaused = false
            });
            onBookStatusChanged(baseToken, quoteToken, quoteScale, minOrderAmount, maxOrderAmount, false);
        }

        /// <summary>
        /// Set the minimum order amount for addLimitOrder
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="minOrderAmount"></param>
        public static void SetMinOrderAmount(UInt160 tokenA, UInt160 tokenB, BigInteger minOrderAmount)
        {
            Assert(minOrderAmount > 0, "Invalid Amount Limit");
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(tokenA, tokenB);
            var bookInfo = GetBook(pairKey);
            Assert(bookInfo.BaseToken is not null, "Book Not Exists");
            Assert(minOrderAmount <= bookInfo.MaxOrderAmount, "Invalid Amount Limit");

            bookInfo.MinOrderAmount = minOrderAmount;
            SetBook(pairKey, bookInfo);
            onBookStatusChanged(bookInfo.BaseToken, bookInfo.QuoteToken, bookInfo.QuoteScale, bookInfo.MinOrderAmount, bookInfo.MaxOrderAmount, bookInfo.IsPaused);
        }

        /// <summary>
        /// Set the maximum trade amount for addLimitOrder
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="maxOrderAmount"></param>
        public static void SetMaxOrderAmount(UInt160 tokenA, UInt160 tokenB, BigInteger maxOrderAmount)
        {
            Assert(maxOrderAmount > 0, "Invalid Amount Limit");
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(tokenA, tokenB);
            var bookInfo = GetBook(pairKey);
            Assert(bookInfo.BaseToken is not null, "Book Not Exists");
            Assert(maxOrderAmount >= bookInfo.MinOrderAmount, "Invalid Amount Limit");

            bookInfo.MaxOrderAmount = maxOrderAmount;
            SetBook(pairKey, bookInfo);
            onBookStatusChanged(bookInfo.BaseToken, bookInfo.QuoteToken, bookInfo.QuoteScale, bookInfo.MinOrderAmount, bookInfo.MaxOrderAmount, bookInfo.IsPaused);
        }

        /// <summary>
        /// Pause an existing order book
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        public static void PauseOrderBook(UInt160 tokenA, UInt160 tokenB)
        {
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(tokenA, tokenB);
            var bookInfo = GetBook(pairKey);
            Assert(bookInfo.BaseToken is not null, "Book Not Exists");
            Assert(bookInfo.IsPaused != true, "Already Paused");

            bookInfo.IsPaused = true;
            SetBook(pairKey, bookInfo);
            onBookStatusChanged(bookInfo.BaseToken, bookInfo.QuoteToken, bookInfo.QuoteScale, bookInfo.MinOrderAmount, bookInfo.MaxOrderAmount, bookInfo.IsPaused);
        }

        /// <summary>
        /// Resume a paused order book
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        public static void ResumeOrderBook(UInt160 tokenA, UInt160 tokenB)
        {
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(tokenA, tokenB);
            var bookInfo = GetBook(pairKey);
            Assert(bookInfo.BaseToken is not null, "Book Not Exists");
            Assert(bookInfo.IsPaused == true, "Not Paused");

            bookInfo.IsPaused = false;
            SetBook(pairKey, bookInfo);
            onBookStatusChanged(bookInfo.BaseToken, bookInfo.QuoteToken, bookInfo.QuoteScale, bookInfo.MinOrderAmount, bookInfo.MaxOrderAmount, bookInfo.IsPaused);
        }

        /// <summary>
        /// 根据要达到的限价簿价格，计算资金池需要输入的Token量
        /// </summary>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="quoteScale"></param>
        /// <param name="reserveIn"></param>
        /// <param name="reserveOut"></param>
        public static BigInteger GetAmountInTillPrice(bool isBuy, BigInteger price, BigInteger quoteScale, BigInteger reserveIn, BigInteger reserveOut)
        {
            Assert(price > 0 && quoteScale > 0 && reserveIn > 0 && reserveOut > 0, "Parameter Invalid");
            var amountIn = BigInteger.Pow(reserveIn, 2) * 9000000;
            if (isBuy) amountIn += reserveIn * reserveOut * price * 3988000000000 / quoteScale;
            else amountIn += reserveIn * reserveOut * quoteScale * 3988000000000 / price;
            return (amountIn.Sqrt() - reserveIn * 1997000) / 1994000;
        }

        public static BigInteger GetAmountInTillPriceWithFundFee(bool isBuy, BigInteger price, BigInteger quoteScale, BigInteger reserveIn, BigInteger reserveOut)
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
        public static BigInteger GetAmountOut(BigInteger amountIn, BigInteger reserveIn, BigInteger reserveOut)
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
        public static BigInteger GetAmountIn(BigInteger amountOut, BigInteger reserveIn, BigInteger reserveOut)
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
        /// <param name="pairContract"></param>
        /// <param name="sender"></param>
        /// <param name="tokenIn"></param>
        /// <param name="tokenOut"></param>
        /// <param name="amountIn"></param>
        /// <param name="amountOut"></param>
        private static void SwapAMM(UInt160 pairContract, UInt160 sender, UInt160 tokenIn, UInt160 tokenOut, BigInteger amountIn, BigInteger amountOut)
        {
            //转入tokenIn
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
