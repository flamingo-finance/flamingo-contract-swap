using System;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services;

namespace FlamingoSwapRouter
{
    partial class FlamingoSwapRouterContract
    {
        //[Syscall("System.Runtime.Notify")]
        //private static extern void Notify(string eventName, params object[] data);

        /// <summary>
        /// 断言
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                onFault(message, null);
                ExecutionEngine.Assert(false);
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
                onFault(message, data);
                ExecutionEngine.Assert(false);
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
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        public static BigInteger[] GetReserves(UInt160 tokenA, UInt160 tokenB)
        {
            var reserveData = (ReservesData)Contract.Call(GetExchangePairWithAssert(tokenA, tokenB), "getReserves", CallFlags.ReadOnly, new object[] { });
            return tokenA.ToUInteger() < tokenB.ToUInteger() ? new BigInteger[] { reserveData.Reserve0, reserveData.Reserve1 } : new BigInteger[] { reserveData.Reserve1, reserveData.Reserve0 };
        }

        /// <summary>
        /// 给定一个价格区间，查询限价簿剩余不能满足的输入量和能够交易的输出量
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="startPrice"></param>
        /// <param name="endPrice"></param>
        /// <param name="amountOutMin"></param>
        public static BigInteger[] GetOrderBookAmountOut(UInt160 tokenA, UInt160 tokenB, BigInteger startPrice, BigInteger endPrice, BigInteger amountOutMin)
        {
            return (BigInteger[])Contract.Call(OrderBook, "getAmountOut", CallFlags.ReadOnly, new object[] { tokenA, tokenB, startPrice, endPrice, amountOutMin });
        }

        /// <summary>
        /// 给定一个价格区间，查询限价簿剩余不能满足的输出量和能够交易的输入量
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="startPrice"></param>
        /// <param name="endPrice"></param>
        /// <param name="amountInMax"></param>
        public static BigInteger[] GetOrderBookAmountIn(UInt160 tokenA, UInt160 tokenB, BigInteger startPrice, BigInteger endPrice, BigInteger amountInMax)
        {
            return (BigInteger[])Contract.Call(OrderBook, "getAmountIn", CallFlags.ReadOnly, new object[] { tokenA, tokenB, startPrice, endPrice, amountInMax });
        }

        /// <summary>
        /// 向限价簿获取交易对的最优报价
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        public static BigInteger GetOrderBookPrice(UInt160 tokenA, UInt160 tokenB, bool isBuy)
        {
            return (BigInteger)Contract.Call(OrderBook, "getMarketPrice", CallFlags.ReadOnly, new object[] { tokenA, tokenB, isBuy });
        }

        /// <summary>
        /// 向限价簿获取交易对的下一级报价
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        public static BigInteger GetOrderBookNextPrice(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price)
        {
            return (BigInteger)Contract.Call(OrderBook, "getNextPrice", CallFlags.ReadOnly, new object[] { tokenA, tokenB, isBuy, price });
        }

        /// <summary>
        /// 查询限价簿交易对是否可用
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        public static bool BookTradable(UInt160 tokenA, UInt160 tokenB)
        {
            return (bool)Contract.Call(OrderBook, "bookTradable", CallFlags.ReadOnly, new object[] { tokenA, tokenB });
        }

        /// <summary>
        /// 向限价簿获取交易对的报价基准代币
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        public static UInt160 GetBaseToken(UInt160 tokenA, UInt160 tokenB)
        {
            return (UInt160)Contract.Call(OrderBook, "getBaseToken", CallFlags.ReadOnly, new object[] { tokenA, tokenB });
        }

        /// <summary>
        /// 向限价簿获取交易对的报价精度
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        public static int GetQuoteDecimals(UInt160 tokenA, UInt160 tokenB)
        {
            return (int)Contract.Call(OrderBook, "getQuoteDecimals", CallFlags.ReadOnly, new object[] { tokenA, tokenB });
        }

        /// <summary>
        /// 向限价簿转发市场交易请求
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="startPrice"></param>
        /// <param name="endPrice"></param>
        /// <param name="amountInMax"></param>
        private static BigInteger SendMarketOrder(UInt160 tokenA, UInt160 tokenB, UInt160 sender, bool isBuy, BigInteger price, BigInteger amount)
        {
            return (BigInteger)Contract.Call(OrderBook, "dealMarketOrder", CallFlags.All, new object[] { tokenA, tokenB, sender, isBuy, price, amount });
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
        /// 安全转账，失败则中断退出
        /// </summary>
        /// <param name="token"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        private static void SafeTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount, byte[] data = null)
        {
            try
            {
                var result = (bool)Contract.Call(token, "transfer", CallFlags.All, new object[] { from, to, amount, data });
                Assert(result, "Transfer Fail in Router", token);
            }
            catch (Exception)
            {
                Assert(false, "Transfer Error in Router", token);
            }
        }


        /// <summary>
        /// 请求转账，未授权则中断退出
        /// </summary>
        /// <param name="token"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        private static void RequestTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount, byte[] data = null)
        {
            try
            {
                var balanceBefore = (BigInteger)Contract.Call(token, "balanceOf", CallFlags.ReadOnly, new object[] { to });
                var result = (bool)Contract.Call(from, "approvedTransfer", CallFlags.All, new object[] { token, to, amount, data });
                var balanceAfter = (BigInteger)Contract.Call(token, "balanceOf", CallFlags.ReadOnly, new object[] { to });
                Assert(result, "Transfer Not Approved in Router", token);
                Assert(balanceAfter == balanceBefore + amount, "Unexpected Transfer in Router", token);
            }
            catch (Exception)
            {
                Assert(false, "Transfer Error in Router", token);
            }
        }


        /// <summary>
        /// Check approval and tranfer as the caller
        /// </summary>
        /// <param name="token"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static bool ApprovedTransfer(UInt160 token, UInt160 to, BigInteger amount, byte[] data = null)
        {
            // Check token
            Assert(token.IsValid && to.IsValid && !to.IsZero && amount >= 0, "Invalid Parameters");

            // Find allowed
            Assert(AllowedOf(token, to) >= amount, "Insufficient Allowed");
            Consume(token, to, amount);

            // Transfer
            var me = Runtime.ExecutingScriptHash;
            SafeTransfer(token, me, to, amount, data);
            return true;
        }

        /// <summary>
        /// Approve some tranfer with a maximal amount
        /// </summary>
        /// <param name="token"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        private static void Approve(UInt160 token, UInt160 to, BigInteger amount)
        {
            Assert(UpdateAllowed(AllowedMapKey + token, to, +amount), "Update Allowed Fail");
        }

        /// <summary>
        /// Decrease the approved amount when transfer happens
        /// </summary>
        /// <param name="token"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        private static void Consume(UInt160 token, UInt160 to, BigInteger amount)
        {
            Assert(UpdateAllowed(AllowedMapKey + token, to, -amount), "Update Allowed Fail");
        }

        /// <summary>
        /// Retrieve the approval when tranfer is completed
        /// </summary>
        /// <param name="token"></param>
        /// <param name="to"></param>
        private static void Retrieve(UInt160 token, UInt160 to)
        {
            Assert(UpdateAllowed(AllowedMapKey + token, to, -AllowedOf(token, to)), "Update Allowed Fail");
        }

        private static BigInteger AllowedOf(UInt160 token, UInt160 to)
        {
            StorageMap allowedMap = new(Storage.CurrentReadOnlyContext, AllowedMapKey + token);
            return (BigInteger)allowedMap.Get(to);
        }

        private static bool UpdateAllowed(string allowedKey, UInt160 owner, BigInteger increment)
        {
            StorageMap allowedMap = new(Storage.CurrentContext, allowedKey);
            BigInteger allowed = (BigInteger)allowedMap[owner];
            allowed += increment;
            if (allowed < 0) return false;
            if (allowed.IsZero)
                allowedMap.Delete(owner);
            else
                allowedMap.Put(owner, allowed);
            return true;
        }


        private static ByteString StorageGet(string key)
        {
            return Storage.Get(Storage.CurrentReadOnlyContext, key);
        }

        private static void StoragePut(string key, string value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
        }

        private static void StoragePut(string key, byte[] value)
        {
            Storage.Put(Storage.CurrentContext, key, (ByteString)value);
        }

        private static void StoragePut(byte[] key, byte[] value)
        {
            Storage.Put(Storage.CurrentContext, key, (ByteString)value);
        }

        private static void StoragePut(string key, ByteString value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
        }

        /// <summary>
        /// 根据报价计算含手续费价格
        /// </summary>
        /// <param name="priceExcludingFee">基准库存</param>
        /// <returns></returns>
        private static BigInteger PriceAddAMMFee(BigInteger priceExcludingFee)
        {
            return (priceExcludingFee * 1000 + 996) / 997;
        }

        private static BigInteger PriceAddBookFee(BigInteger priceExcludingFee)
        {
            return (priceExcludingFee * 10000 + 9984) / 9985;
        }

        /// <summary>
        /// 根据含手续费价格计算原报价
        /// </summary>
        /// <param name="priceIncludingFee">基准库存</param>
        /// <returns></returns>
        private static BigInteger PriceRemoveAMMFee(BigInteger priceIncludingFee)
        {
            return priceIncludingFee * 997 / 1000;
        }

        private static BigInteger PriceRemoveBookFee(BigInteger priceIncludingFee)
        {
            return priceIncludingFee * 9985 / 10000;
        }
    }
}
