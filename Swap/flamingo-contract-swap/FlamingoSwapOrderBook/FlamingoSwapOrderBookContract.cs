using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using System.Numerics;
using System.ComponentModel;

namespace FlamingoSwapOrderBook
{
    [DisplayName("FlamingoSwapOrderBook")]
    [ManifestExtra("Author", "Flamingo Finance")]
    [ManifestExtra("Email", "developer@flamingo.finance")]
    [ManifestExtra("Description", "This is a Flamingo Contract")]
    [ContractPermission("*")]
    public partial class FlamingoSwapOrderBookContract : SmartContract
    {
        /// <summary>
        /// Deal and add limit order base on input strategy
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="sender"></param>
        /// <param name="isBuy"></param>
        /// <param name="amount">Total buy/sell(real get/pay) amount of the limit order</param>
        /// <param name="price">Price limit of the order</param>
        /// <param name="bookAmount">Expected amount to buy/sell(in-book amount) in book before amm</param>
        /// <param name="bookPrice">Price limit of bookAmount part</param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static ByteString RouteLimitOrder(UInt160 tokenA, UInt160 tokenB, UInt160 sender, bool isBuy, BigInteger amount, BigInteger price, BigInteger bookAmount, BigInteger bookPrice, BigInteger deadLine)
        {
            Assert(amount > 0 && price > 0 && bookAmount >= 0 && amount >= bookAmount, "Invalid Parameters");
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(!BookPaused(pairKey), "Book Is Paused");
            Assert(Runtime.CheckWitness(sender), "No Authorization");
            Assert(ContractManagement.GetContract(sender) == null, "Forbidden");
            if (bookAmount > 0) Assert((isBuy && price >= bookPrice) || (!isBuy && price <= bookPrice), "BookPrice Beyond Limit");
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded The Deadline");

            // Market order
            var leftAmount = amount;
            if (isBuy) leftAmount -= bookAmount > 0 ? (bookAmount - DealMarketOrderInternal(pairKey, sender, isBuy, bookPrice, bookAmount, false)) * 997 / 1000 : 0;
            else leftAmount -= bookAmount > 0 ? bookAmount - DealMarketOrderInternal(pairKey, sender, isBuy, bookPrice, bookAmount, false) : 0;
            if (leftAmount == 0) return null;

            // Swap AMM
            var book = GetOrderBook(pairKey);
            Assert(book.baseToken.IsAddress() && book.quoteToken.IsAddress(), "Invalid Trade Pair");
            var pairContract = GetExchangePairWithAssert(tokenA, tokenB);
            var hasFundFee = HasFundAddress(pairContract);

            var ammReverse = isBuy
                ? GetReserves(pairContract, book.quoteToken, book.baseToken)
                : GetReserves(pairContract, book.baseToken, book.quoteToken);
            var amountIn = hasFundFee
                ? GetAmountInTillPriceWithFundFee(isBuy, price, book.quoteScale, ammReverse[0], ammReverse[1])
                : GetAmountInTillPrice(isBuy, price, book.quoteScale, ammReverse[0], ammReverse[1]);
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

            if (amountOut > 0) 
            {
                SwapAMM(pairContract, sender, isBuy ? book.quoteToken : book.baseToken, isBuy ? book.baseToken : book.quoteToken, amountIn, amountOut);
                leftAmount -= isBuy ? amountOut : amountIn;
            }

            // Add limit order
            if (leftAmount < book.minOrderAmount || leftAmount > book.maxOrderAmount) return null;
            var me = Runtime.ExecutingScriptHash;
            if (isBuy) SafeTransfer(book.quoteToken, sender, me, leftAmount * price / book.quoteScale);
            else SafeTransfer(book.baseToken, sender, me, leftAmount);

            var id = GetUnusedID();
            Assert(InsertOrder(pairKey, book, id, new LimitOrder(){
                maker = sender,
                price = price,
                amount = leftAmount
            }, isBuy), "Add Order Fail");

            // Add receipt
            SetReceipt(sender, id, new OrderReceipt(){
                baseToken = book.baseToken,
                quoteToken = book.quoteToken,
                id = id,
                time = Runtime.Time,
                isBuy = isBuy,
                totalAmount = amount
            });
            onOrderStatusChanged(book.baseToken, book.quoteToken, id, !!isBuy, sender, price, leftAmount);
            return id;
        }

        /// <summary>
        /// Deal market order based on input strategy
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="sender"></param>
        /// <param name="isBuy"></param>
        /// <param name="amount">Total buy/sell(real get/pay) amount of the limit order</param>
        /// <param name="slippage">The amount limit of final receive/payment(real get/pay)</param>
        /// <param name="bookAmount">Expected amount to buy/sell(in-book amount) in book before amm</param>
        /// <param name="bookPrice">Price limit of bookAmount part</param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static bool RouteMarketOrder(UInt160 tokenA, UInt160 tokenB, UInt160 sender, bool isBuy, BigInteger amount, BigInteger slippage, BigInteger bookAmount, BigInteger bookPrice, BigInteger deadLine)
        {
            Assert(amount > 0 && slippage > 0 && bookAmount >= 0 && amount >= bookAmount, "Invalid Parameters");
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(!BookPaused(pairKey), "Book is Paused");
            Assert(Runtime.CheckWitness(sender), "No Authorization");
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded the Deadline");

            // Market order
            var book = GetOrderBook(pairKey);
            Assert(book.baseToken.IsAddress() && book.quoteToken.IsAddress(), "Invalid Trade Pair");
            var price = slippage * book.quoteScale / amount;
            if (bookAmount > 0) Assert((isBuy && price >= bookPrice) || (!isBuy && price <= bookPrice), "BookPrice Beyond Limit");

            var quoteAmount = BigInteger.Zero;
            if (bookAmount > 0)
            {
                var balanceBefore = GetBalanceOf(book.quoteToken, sender);
                if (isBuy) amount -= (bookAmount - DealMarketOrderInternal(pairKey, sender, isBuy, bookPrice, bookAmount, false)) * 997 / 1000;
                else amount -= bookAmount - DealMarketOrderInternal(pairKey, sender, isBuy, bookPrice, bookAmount, false);
                var balanceAfter = GetBalanceOf(book.quoteToken, sender);
                quoteAmount = isBuy ? balanceBefore - balanceAfter : balanceAfter - balanceBefore;
                if (amount == 0)
                {
                    Assert(isBuy ? quoteAmount <= slippage : quoteAmount >= slippage, "Insufficient Slippage");
                    return true;
                }
            }

            // Swap AMM
            var pairContract = GetExchangePairWithAssert(tokenA, tokenB);
            var ammReverse = GetReserves(pairContract, book.baseToken, book.quoteToken);

            var amountIn = isBuy ? GetAmountIn(amount, ammReverse[1], ammReverse[0]) : amount;
            var amountOut = isBuy ? amount : GetAmountOut(amount, ammReverse[0], ammReverse[1]);

            if (amountOut > 0) SwapAMM(pairContract, sender, isBuy ? book.quoteToken : book.baseToken, isBuy ? book.baseToken : book.quoteToken, amountIn, amountOut);
            Assert(isBuy ? amountIn + quoteAmount <= slippage : amountOut + quoteAmount >= slippage, "Insufficient Slippage");
            return true;
        }

        #region DEX like API
        /// <summary>
        /// Register a new book
        /// </summary>
        /// <param name="baseToken"></param>
        /// <param name="quoteToken"></param>
        /// <param name="quoteDecimals"></param>
        /// <param name="minOrderAmount"></param>
        /// <param name="maxOrderAmount"></param>
        /// <returns></returns>
        public static bool RegisterOrderBook(UInt160 baseToken, UInt160 quoteToken, uint quoteDecimals, BigInteger minOrderAmount, BigInteger maxOrderAmount)
        {
            Assert(baseToken.IsAddress() && quoteToken.IsAddress(), "Invalid Address");
            Assert(baseToken != quoteToken, "Invalid Trade Pair");
            Assert(minOrderAmount > 0 && maxOrderAmount > 0 && minOrderAmount <= maxOrderAmount, "Invalid Amount Limit");
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            var quoteScale = BigInteger.Pow(10, (int)quoteDecimals);
            Assert(!BookExists(pairKey), "Book Already Registered");
            SetOrderBook(pairKey, new OrderBook(){
                baseToken = baseToken,
                quoteToken = quoteToken,
                quoteScale = quoteScale,
                minOrderAmount = minOrderAmount,
                maxOrderAmount = maxOrderAmount
            });
            onBookStatusChanged(baseToken, quoteToken, quoteScale, minOrderAmount, maxOrderAmount, BookPaused(pairKey));
            return true;
        }

        /// <summary>
        /// Set the minimum order amount for addLimitOrder
        /// </summary>
        /// <param name="baseToken"></param>
        /// <param name="quoteToken"></param>
        /// <param name="minOrderAmount"></param>
        /// <returns></returns>
        public static bool SetMinOrderAmount(UInt160 baseToken, UInt160 quoteToken, BigInteger minOrderAmount)
        {
            Assert(minOrderAmount > 0, "Invalid Amount Limit");
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            var book = GetOrderBook(pairKey);
            Assert(book.baseToken == baseToken && book.quoteToken == quoteToken, "Invalid Trade Pair");
            Assert(minOrderAmount <= book.maxOrderAmount, "Invalid Amount Limit");

            book.minOrderAmount = minOrderAmount;
            SetOrderBook(pairKey, book);
            onBookStatusChanged(book.baseToken, book.quoteToken, book.quoteScale, book.minOrderAmount, book.maxOrderAmount, BookPaused(pairKey));
            return true;
        }

        /// <summary>
        /// Set the maximum trade amount for addLimitOrder
        /// </summary>
        /// <param name="baseToken"></param>
        /// <param name="quoteToken"></param>
        /// <param name="maxOrderAmount"></param>
        /// <returns></returns>
        public static bool SetMaxOrderAmount(UInt160 baseToken, UInt160 quoteToken, BigInteger maxOrderAmount)
        {
            Assert(maxOrderAmount > 0, "Invalid Amount Limit");
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            var book = GetOrderBook(pairKey);
            Assert(book.baseToken == baseToken && book.quoteToken == quoteToken, "Invalid Trade Pair");
            Assert(maxOrderAmount >= book.minOrderAmount, "Invalid Amount Limit");

            book.maxOrderAmount = maxOrderAmount;
            SetOrderBook(pairKey, book);
            onBookStatusChanged(book.baseToken, book.quoteToken, book.quoteScale, book.minOrderAmount, book.maxOrderAmount, BookPaused(pairKey));
            return true;
        }

        /// <summary>
        /// Pause an existing order book
        /// </summary>
        /// <param name="baseToken"></param>
        /// <param name="quoteToken"></param>
        /// <returns></returns>
        public static bool PauseOrderBook(UInt160 baseToken, UInt160 quoteToken)
        {
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            var book = GetOrderBook(pairKey);
            Assert(book.baseToken == baseToken && book.quoteToken == quoteToken, "Invalid Trade Pair");
            Assert(!BookPaused(pairKey), "Book Already Paused");

            SetPaused(pairKey);
            onBookStatusChanged(book.baseToken, book.quoteToken, book.quoteScale, book.minOrderAmount, book.maxOrderAmount, BookPaused(pairKey));
            return true;
        }

        /// <summary>
        /// Resume a paused order book
        /// </summary>
        /// <param name="baseToken"></param>
        /// <param name="quoteToken"></param>
        /// <returns></returns>
        public static bool ResumeOrderBook(UInt160 baseToken, UInt160 quoteToken)
        {
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            var book = GetOrderBook(pairKey);
            Assert(book.baseToken == baseToken && book.quoteToken == quoteToken, "Invalid Trade Pair");
            Assert(BookPaused(pairKey), "Book Not Paused");

            RemovePaused(pairKey);
            onBookStatusChanged(book.baseToken, book.quoteToken, book.quoteScale, book.minOrderAmount, book.maxOrderAmount, BookPaused(pairKey));
            return true;
        }

        /// <summary>
        /// Add a new order into orderbook but try deal it first
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="maker"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns>Null or a new order id</returns>
        public static ByteString AddLimitOrder(UInt160 tokenA, UInt160 tokenB, UInt160 maker, bool isBuy, BigInteger price, BigInteger amount)
        {
            // Check parameters
            Assert(price > 0 && amount > 0, "Invalid Parameters");
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(!BookPaused(pairKey), "Book Is Paused");
            Assert(Runtime.CheckWitness(maker), "No Authorization");
            Assert(ContractManagement.GetContract(maker) == null, "Forbidden");

            // Deposit token
            var book = GetOrderBook(pairKey);
            Assert(book.baseToken.IsAddress() && book.quoteToken.IsAddress(), "Invalid Trade Pair");
            if (amount < book.minOrderAmount || amount > book.maxOrderAmount) return null;
            var me = Runtime.ExecutingScriptHash;
            if (isBuy) SafeTransfer(book.quoteToken, maker, me, amount * price / book.quoteScale);
            else SafeTransfer(book.baseToken, maker, me, amount);

            // Do add
            var id = GetUnusedID();
            Assert(InsertOrder(pairKey, book, id, new LimitOrder(){
                maker = maker,
                price = price,
                amount = amount
            }, isBuy), "Add Order Fail");

            // Add receipt
            SetReceipt(maker, id, new OrderReceipt(){
                baseToken = book.baseToken,
                quoteToken = book.quoteToken,
                id = id,
                time = Runtime.Time,
                isBuy = isBuy,
                totalAmount = amount
            });
            onOrderStatusChanged(book.baseToken, book.quoteToken, id, !!isBuy, maker, price, amount);
            return id;
        }

        /// <summary>
        /// Add a new order into orderbook with an expected parent order id
        /// </summary>
        /// <param name="parentID"></param>
        /// <param name="maker"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns>Null or a new order id</returns>
        public static ByteString AddLimitOrderAt(ByteString parentID, UInt160 maker, BigInteger price, BigInteger amount)
        {
            // Check parameters
            Assert(price > 0 && amount > 0, "Invalid Parameters");
            var parent = GetOrder(parentID);
            Assert(parent.maker.IsAddress(), "Parent Not Exists");
            var receipt = GetReceipt(parent.maker, parentID);
            var pairKey = GetPairKey(receipt.baseToken, receipt.quoteToken);
            Assert(!BookPaused(pairKey), "Book Is Paused");
            Assert(Runtime.CheckWitness(maker), "No Authorization");
            Assert(ContractManagement.GetContract(maker) == null, "Forbidden");

            // Check amount
            var book = GetOrderBook(pairKey);
            Assert(amount >= book.minOrderAmount && amount <= book.maxOrderAmount, "Invalid Limit Order Amount");

            // Deposit token
            var me = Runtime.ExecutingScriptHash;
            if (receipt.isBuy) SafeTransfer(receipt.quoteToken, maker, me, amount * price / book.quoteScale);
            else SafeTransfer(receipt.baseToken, maker, me, amount);

            // Insert new order
            var id = GetUnusedID();
            Assert(InsertOrderAt(parentID, id, new LimitOrder(){
                maker = maker,
                price = price,
                amount = amount
            }, receipt.isBuy), "Add Order Fail");

            // Add receipt
            SetReceipt(maker, id, new OrderReceipt(){
                baseToken = receipt.baseToken,
                quoteToken = receipt.quoteToken,
                id = id,
                time = Runtime.Time,
                isBuy = receipt.isBuy,
                totalAmount = amount
            });
            onOrderStatusChanged(receipt.baseToken, receipt.quoteToken, id, !!receipt.isBuy, maker, price, amount);
            return id;
        }

        /// <summary>
        /// Cancel a limit order with its id
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool CancelOrder(UInt160 tokenA, UInt160 tokenB, bool isBuy, ByteString id)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            var order = GetOrder(id);
            Assert(order.maker.IsAddress(), "Order Not Exists");
            Assert(Runtime.CheckWitness(order.maker), "No Authorization");

            // Do remove
            var book = GetOrderBook(pairKey);
            Assert(RemoveOrder(pairKey, book, id, isBuy), "Remove Order Fail");

            // Remove receipt
            DeleteReceipt(order.maker, id);
            onOrderStatusChanged(book.baseToken, book.quoteToken, id, !!isBuy, order.maker, order.price, 0);

            // Withdraw token
            var me = Runtime.ExecutingScriptHash;
            if (isBuy) SafeTransfer(book.quoteToken, me, order.maker, order.amount * order.price / book.quoteScale);
            else SafeTransfer(book.baseToken, me, order.maker, order.amount);
            return true;
        }

        /// <summary>
        /// Cancel a limit order with its id and parent order id
        /// </summary>
        /// <param name="parentID"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool CancelOrderAt(ByteString parentID, ByteString id)
        {
            var order = GetOrder(id);
            Assert(order.maker.IsAddress(), "Order Not Exists");
            Assert(Runtime.CheckWitness(order.maker), "No Authorization");

            // Do remove
            var receipt = GetReceipt(order.maker, id);
            var pairKey = GetPairKey(receipt.baseToken, receipt.quoteToken);
            var book = GetOrderBook(pairKey);

            Assert(RemoveOrderAt(parentID, id), "Remove Order Fail");

            // Remove receipt
            DeleteReceipt(order.maker, id);
            onOrderStatusChanged(receipt.baseToken, receipt.quoteToken, id, !!receipt.isBuy, order.maker, order.price, 0);

            // Withdraw token
            var me = Runtime.ExecutingScriptHash;
            if (receipt.isBuy) SafeTransfer(receipt.quoteToken, me, order.maker, order.amount * order.price / book.quoteScale);
            else SafeTransfer(receipt.baseToken, me, order.maker, order.amount);
            return true;
        }

        /// <summary>
        /// Try get the parent order id of an existing order
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [Safe]
        public static ByteString GetParentOrderID(UInt160 tokenA, UInt160 tokenB, bool isBuy, ByteString id)
        {
            var pairKey = GetPairKey(tokenA, tokenB);
            return GetParentID(pairKey, isBuy, id);
        }

        /// <summary>
        /// Get first N limit orders and their details, start from pos
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="pos"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        [Safe]
        public static OrderReceipt[] GetFirstNOrders(UInt160 tokenA, UInt160 tokenB, bool isBuy, uint pos, uint n)
        {
            var results = new OrderReceipt[n];

            var pairKey = GetPairKey(tokenA, tokenB);
            var book = GetOrderBook(pairKey);
            var firstID = isBuy ? book.firstBuyID : book.firstSellID;
            if (firstID is null) return results;

            var currentOrderID = firstID;
            var currentOrder = GetOrder(currentOrderID);
            for (int i = 0; i < pos + n; i++)
            {
                if (i >= pos)
                {
                    var receipt = GetReceipt(currentOrder.maker, currentOrderID);
                    receipt.maker = currentOrder.maker;
                    receipt.price = currentOrder.price;
                    receipt.leftAmount = currentOrder.amount;
                    results[i - pos] = receipt;

                    if (currentOrder.nextID is null) break;
                }
                currentOrderID = currentOrder.nextID;
                currentOrder = GetOrder(currentOrder.nextID);
            }

            return results;
        }

        /// <summary>
        /// Get first N limit orders and their details, start from orderID
        /// </summary>
        /// <param name="orderID"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        [Safe]
        public static OrderReceipt[] GetFirstNOrders(ByteString orderID, uint n)
        {
            var results = new OrderReceipt[n];

            var currentOrderID = orderID;
            var currentOrder = GetOrder(currentOrderID);
            if (!currentOrder.maker.IsAddress()) return results;
            for (int i = 0; i < n; i++)
            {
                var receipt = GetReceipt(currentOrder.maker, currentOrderID);
                receipt.maker = currentOrder.maker;
                receipt.price = currentOrder.price;
                receipt.leftAmount = currentOrder.amount;
                results[i] = receipt;

                if (currentOrder.nextID is null) break;

                currentOrderID = currentOrder.nextID;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return results;
        }

        /// <summary>
        /// Get N orders of maker and their details, start from pos
        /// </summary>
        /// <param name="maker"></param>
        /// <param name="pos"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        [Safe]
        public static OrderReceipt[] GetOrdersOf(UInt160 maker, uint pos, uint n)
        {
            var results = new OrderReceipt[n];
            var iterator = ReceiptsOf(maker);
            // Make up details
            for (int i = 0; i < pos + n; i++)
            {
                if (iterator.Next() && i >= pos)
                {
                    results[i - pos] = (OrderReceipt)iterator.Value;
                    var order = GetOrder(results[i - pos].id);
                    results[i - pos].maker = order.maker;
                    results[i - pos].price = order.price;
                    results[i - pos].leftAmount = order.amount;
                }
            }
            return results;
        }

        /// <summary>
        /// Get N orders of maker and their details, start from orderID
        /// </summary>
        /// <param name="orderID"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        [Safe]
        public static OrderReceipt[] GetOrdersOf(ByteString orderID, uint n)
        {
            var results = new OrderReceipt[n];
            var order = GetOrder(orderID);
            if (!order.maker.IsAddress()) return results;
            var iterator = ReceiptsOf(order.maker);
            // Make up details
            while (iterator.Next())
            {
                var receipt = (OrderReceipt)iterator.Value;
                if (receipt.id.Equals(orderID))
                {
                    for (int i = 0; i < n; i++)
                    {
                        results[i] = receipt;
                        order = GetOrder(results[i].id);
                        results[i].maker = order.maker;
                        results[i].price = order.price;
                        results[i].leftAmount = order.amount;

                        if (!iterator.Next()) break;
                        receipt = (OrderReceipt)iterator.Value;
                    }
                    return results;
                }
            }
            return results;
        }

        /// <summary>
        /// Get the total reverse of tradable orders with an expected price
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <returns></returns>
        [Safe]
        public static BigInteger GetTotalTradable(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price)
        {
            var pairKey = GetPairKey(tokenA, tokenB);
            if (BookPaused(pairKey)) return 0;
            return GetTotalTradableInternal(pairKey, isBuy, price);
        }

        private static BigInteger GetTotalTradableInternal(byte[] pairKey, bool isBuy, BigInteger price)
        {
            var totalTradable = BigInteger.Zero;
            var book = GetOrderBook(pairKey);
            var currentID = isBuy ? book.firstSellID : book.firstBuyID;
            if (currentID is null) return totalTradable;
            while (currentID is not null)
            {
                var currentOrder = GetOrder(currentID);
                if ((isBuy && currentOrder.price > price) || (!isBuy && currentOrder.price < price)) break;
                totalTradable += currentOrder.amount;
                currentID = currentOrder.nextID;
            }
            return totalTradable;
        }

        /// <summary>
        /// Try to match without real payment
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        [Safe]
        public static BigInteger[] MatchOrder(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price, BigInteger amount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            if (BookPaused(pairKey)) return new BigInteger[] { amount, 0 };
            return MatchOrderInternal(pairKey, isBuy, price, amount);
        }

        private static BigInteger[] MatchOrderInternal(byte[] pairKey, bool isBuy, BigInteger price, BigInteger amount)
        {
            var totalPayment = BigInteger.Zero;
            var book = GetOrderBook(pairKey);
            var currentID = isBuy ? book.firstSellID : book.firstBuyID;
            if (currentID is null) return new BigInteger[] { amount, 0 };
            var currentOrder = GetOrder(currentID);

            while (amount > 0)
            {
                // Check price
                if ((isBuy && currentOrder.price > price) || (!isBuy && currentOrder.price < price)) break;

                if (currentOrder.amount <= amount) 
                {
                    totalPayment += currentOrder.amount * currentOrder.price / book.quoteScale;
                    amount -= currentOrder.amount;
                }
                else
                {
                    totalPayment += amount * currentOrder.price / book.quoteScale;
                    amount = 0;
                }

                if (currentOrder.nextID is null) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }

            // A least payment for buyer
            if (isBuy && totalPayment == 0) totalPayment += 1;
            return new BigInteger[] { amount, totalPayment };
        }

        /// <summary>
        /// Try to match without real payment
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="quoteAmount"></param>
        /// <returns>Left amount and tradable base</returns>
        [Safe]
        public static BigInteger[] MatchQuote(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price, BigInteger quoteAmount)
        {
            var pairKey = GetPairKey(tokenA, tokenB);
            if (BookPaused(pairKey)) return new BigInteger[] { quoteAmount, 0 };
            return MatchQuoteInternal(pairKey, isBuy, price, quoteAmount);
        }

        private static BigInteger[] MatchQuoteInternal(byte[] pairKey, bool isBuy, BigInteger price, BigInteger quoteAmount)
        {
            var totalTradable = BigInteger.Zero;
            var book = GetOrderBook(pairKey);
            var currentID = isBuy ? book.firstSellID : book.firstBuyID;
            if (currentID is null) return new BigInteger[] { quoteAmount, 0 };
            var currentOrder = GetOrder(currentID);

            while (quoteAmount > 0)
            {
                // Check price
                if ((isBuy && currentOrder.price > price) || (!isBuy && currentOrder.price < price)) break;
                var payment = currentOrder.amount * currentOrder.price / book.quoteScale;
                if (payment <= quoteAmount) 
                {
                    totalTradable += currentOrder.amount;
                    quoteAmount -= payment;
                }
                else
                {
                    // For buyer, real payment <= expected
                    if (isBuy) totalTradable += quoteAmount * book.quoteScale / currentOrder.price;
                    // For seller, real payment >= expected
                    else totalTradable += (quoteAmount * book.quoteScale + currentOrder.price - 1) / currentOrder.price;
                    quoteAmount = 0;
                }

                if (currentOrder.nextID is null) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return new BigInteger[] { quoteAmount, totalTradable };
        }

        /// <summary>
        /// Try to make a market deal with orderbook, taker is not a contract
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="taker"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns>Left amount</returns>
        public static BigInteger DealMarketOrder(UInt160 tokenA, UInt160 tokenB, UInt160 taker, bool isBuy, BigInteger price, BigInteger amount)
        {
            // Check parameters
            Assert(price > 0 && amount > 0, "Invalid Parameters");
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(!BookPaused(pairKey), "Book Is Paused");
            Assert(Runtime.CheckWitness(taker), "No Authorization");
            Assert(ContractManagement.GetContract(taker) == null, "Forbidden");

            return DealMarketOrderInternal(pairKey, taker, isBuy, price, amount, false);
        }

        /// <summary>
        /// Try to make a market deal with orderbook, taker is a contract
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns>Left amount</returns>
        public static BigInteger DealMarketOrder(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price, BigInteger amount)
        {
            // Check parameters
            Assert(price > 0 && amount > 0, "Invalid Parameters");
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(!BookPaused(pairKey), "Book Is Paused");
            var caller = Runtime.CallingScriptHash;
            Assert(ContractManagement.GetContract(caller) != null, "Forbidden"); 

            return DealMarketOrderInternal(pairKey, caller, isBuy, price, amount, true);
        }

        private static BigInteger DealMarketOrderInternal(byte[] pairKey, UInt160 taker, bool isBuy, BigInteger price, BigInteger leftAmount, bool shouldRequest)
        {
            // Check if can deal
            var book = GetOrderBook(pairKey);
            Assert(book.baseToken.IsAddress() && book.quoteToken.IsAddress(), "Invalid Trade Pair");
            var firstID = isBuy ? book.firstSellID : book.firstBuyID;
            if (firstID is null) return leftAmount;
            var firstOrder = GetOrder(firstID);
            var canDeal = (isBuy && firstOrder.price <= price) || (!isBuy && firstOrder.price >= price);
            if (!canDeal) return leftAmount;

            var me = Runtime.ExecutingScriptHash;
            var fundAddress = GetFundAddress();

            var quoteFee = BigInteger.Zero;
            var baseFee = BigInteger.Zero;

            var takerReceive = BigInteger.Zero;
            var takerPayment = BigInteger.Zero;
            var makerReceive = new Map<UInt160, BigInteger>();

            var currentID = firstID;
            while (currentID is not null)
            {
                var currentOrder = GetOrder(currentID);
                if ((isBuy && currentOrder.price > price) || (!isBuy && currentOrder.price < price)) break;

                BigInteger quoteAmount;
                BigInteger baseAmount;
                BigInteger quotePayment;
                BigInteger basePayment;

                if (currentOrder.amount <= leftAmount)
                {
                    // Full-fill
                    quoteAmount = currentOrder.amount * currentOrder.price / book.quoteScale;
                    baseAmount = currentOrder.amount;

                    // Remove full-fill order
                    DeleteOrder(currentID);
                    DeleteReceipt(currentOrder.maker, currentID);
                    firstID = currentOrder.nextID;

                    onOrderStatusChanged(book.baseToken, book.quoteToken, currentID, !isBuy, currentOrder.maker, currentOrder.price, 0);
                    leftAmount -= currentOrder.amount;
                }
                else
                {
                    // Part-fill
                    quoteAmount = leftAmount * currentOrder.price / book.quoteScale;
                    baseAmount = leftAmount;

                    // Update order
                    currentOrder.amount -= leftAmount;
                    SetOrder(currentID, currentOrder);

                    onOrderStatusChanged(book.baseToken, book.quoteToken, currentID, !isBuy, currentOrder.maker, currentOrder.price, currentOrder.amount);
                    leftAmount = 0;
                }

                // Record payment
                quotePayment = quoteAmount * 997 / 1000;
                basePayment = baseAmount * 997 / 1000;
                quoteFee += quoteAmount - quotePayment;
                baseFee += baseAmount - basePayment;

                if (isBuy)
                {
                    takerPayment += quoteAmount;
                    takerReceive += basePayment;
                    if (makerReceive.HasKey(currentOrder.maker)) makerReceive[currentOrder.maker] += quotePayment;
                    else makerReceive[currentOrder.maker] = quotePayment;
                }
                else
                {
                    takerPayment += baseAmount;
                    takerReceive += quotePayment;
                    if (makerReceive.HasKey(taker)) makerReceive[taker] += basePayment;
                    else makerReceive[taker] = basePayment;
                }

                // Check if still tradable
                if (leftAmount == 0) break;
                currentID = currentOrder.nextID;
            }

            // Update book if necessary
            if (isBuy && book.firstSellID != firstID)
            {
                book.firstSellID = firstID;
                SetOrderBook(pairKey, book);
            }
            if (!isBuy && book.firstBuyID != firstID)
            {
                book.firstBuyID = firstID;
                SetOrderBook(pairKey, book);
            }

            // Do transfer
            if (takerPayment == 0) takerPayment += 1;
            if (isBuy)
            {
                if (shouldRequest) RequestTransfer(book.quoteToken, taker, me, takerPayment);
                else SafeTransfer(book.quoteToken, taker, me, takerPayment);
                SafeTransfer(book.baseToken, me, taker, takerReceive);
                foreach (var toAddress in makerReceive.Keys) SafeTransfer(book.quoteToken, me, toAddress, makerReceive[toAddress]);
            }
            else
            {
                if (shouldRequest) RequestTransfer(book.baseToken, taker, me, takerPayment);
                else SafeTransfer(book.baseToken, taker, me, takerPayment);
                SafeTransfer(book.quoteToken, me, taker, takerReceive);
                foreach (var toAddress in makerReceive.Keys) SafeTransfer(book.baseToken, me, toAddress, makerReceive[toAddress]);
            }
            
            StageFundFee(book.baseToken, baseFee);
            StageFundFee(book.quoteToken, quoteFee);

            return leftAmount;
        }

        /// <summary>
        /// Deal a whole limit order with it id and parent id
        /// </summary>
        /// <param name="taker"></param>
        /// <param name="parentID"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool DealMarketOrderAt(UInt160 taker, ByteString parentID, ByteString id)
        {
            // Check parameters
            Assert(Runtime.CheckWitness(taker), "No Authorization");
            var order = GetOrder(id);
            Assert(order.maker.IsAddress(), "Order Not Exists");
            var receipt = GetReceipt(order.maker, id);
            var pairKey = GetPairKey(receipt.baseToken, receipt.quoteToken);
            Assert(!BookPaused(pairKey), "Book Is Paused");

            // Do deal
            var me = Runtime.ExecutingScriptHash;
            var quoteScale = GetOrderBook(pairKey).quoteScale;
            var fundAddress = GetFundAddress();

            var baseAmount = order.amount;
            var quoteAmount = order.amount * order.price / quoteScale;
            var basePayment = baseAmount * 997 / 1000;
            var quotePayment = quoteAmount * 997 / 1000;
            var baseFee = baseAmount - basePayment;
            var quoteFee = quoteAmount - quotePayment;

            if (receipt.isBuy) SafeTransfer(receipt.baseToken, taker, me, baseAmount);
            else SafeTransfer(receipt.quoteToken, taker, me, quoteAmount);

            // Remove order and receipt
            Assert(RemoveOrderAt(parentID, id), "Remove Order Fail");
            DeleteReceipt(order.maker, id);
            onOrderStatusChanged(receipt.baseToken, receipt.quoteToken, id, !!receipt.isBuy, order.maker, order.price, 0);

            // Transfer
            if (receipt.isBuy)
            {
                SafeTransfer(receipt.baseToken, me, order.maker, basePayment);
                SafeTransfer(receipt.quoteToken, me, taker, quotePayment);
            }
            else
            {
                SafeTransfer(receipt.baseToken, me, taker, basePayment);
                SafeTransfer(receipt.quoteToken, me, order.maker, quotePayment);
            }

            StageFundFee(receipt.baseToken, baseFee);
            StageFundFee(receipt.quoteToken, quoteFee);
            return true;
        }

        /// <summary>
        /// Claim the staged fundfee payment to fund address
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns></returns>
        public static bool ClaimFundFee(UInt160[] tokens)
        {
            var fundAddress = GetFundAddress();
            if (fundAddress is null) return false;
            var me = Runtime.ExecutingScriptHash;
            foreach (var token in tokens)
            {
                var amount = GetStagedFundFee(token);
                if (amount > 0)
                {
                    CleanStagedFundFee(token);
                    SafeTransfer(token, me, fundAddress, amount);
                }
            }
            return true;
        }

        /// <summary>
        /// Price reporter
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <returns></returns>
        [Safe]
        public static BigInteger GetMarketPrice(UInt160 tokenA, UInt160 tokenB, bool isBuy)
        {
            var pairKey = GetPairKey(tokenA, tokenB);
            var book = GetOrderBook(pairKey);
            var firstID = isBuy ? book.firstSellID : book.firstBuyID;
            if (firstID is null) return 0;
            return GetOrder(firstID).price;
        }

        /// <summary>
        /// Get trade pair details
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        [Safe]
        public static OrderBook GetBookInfo(UInt160 tokenA, UInt160 tokenB)
        {
            var pairKey = GetPairKey(tokenA, tokenB);
            return GetOrderBook(pairKey);
        }

        /// <summary>
        /// Check if a pair of token is tradable
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        [Safe]
        public static bool BookTradable(UInt160 tokenA, UInt160 tokenB)
        {
            var pairKey = GetPairKey(tokenA, tokenB);
            return BookExists(pairKey) && !BookPaused(pairKey);
        }
        #endregion

        #region AMM like API
        /// <summary>
        /// Calculate amountOut with amountIn
        /// </summary>
        /// <param name="tokenFrom"></param>
        /// <param name="tokenTo"></param>
        /// <param name="price"></param>
        /// <param name="amountIn"></param>
        /// <returns>Unsatisfied amountIn and amountOut</returns>
        [Safe]
        public static BigInteger[] GetAmountOut(UInt160 tokenFrom, UInt160 tokenTo, BigInteger price, BigInteger amountIn)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            if (BookPaused(pairKey)) return new BigInteger[] { amountIn, 0 };
            var book = GetOrderBook(pairKey);
            var isBuy = tokenFrom == book.quoteToken;
            if (isBuy)
            {
                var result = MatchQuoteInternal(pairKey, isBuy, price, amountIn);
                return new BigInteger[]{ result[0], result[1] * 997 / 1000 };   // 0.3% fee
            }
            else
            {
                var result = MatchOrderInternal(pairKey, isBuy, price, amountIn);
                return new BigInteger[]{ result[0], result[1] * 997 / 1000 };   // 0.3% fee
            }
        }

        /// <summary>
        /// Calculate amountOut with amountIn
        /// </summary>
        /// <param name="tokenFrom"></param>
        /// <param name="tokenTo"></param>
        /// <param name="price"></param>
        /// <param name="amountOut"></param>
        /// <returns>Unsatisfied amountOut and amountIn</returns>
        [Safe]
        public static BigInteger[] GetAmountIn(UInt160 tokenFrom, UInt160 tokenTo, BigInteger price, BigInteger amountOut)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            if (BookPaused(pairKey)) return new BigInteger[] { amountOut, 0 };
            var book = GetOrderBook(pairKey);
            var isBuy = tokenFrom == book.quoteToken;
            if (isBuy)
            {
                var result = MatchOrderInternal(pairKey, isBuy, price, (amountOut * 1000 + 996) / 997);   // 0.3% fee
                return new BigInteger[]{ result[0] * 997 / 1000, result[1] };
            }
            else
            {
                var result = MatchQuoteInternal(pairKey, isBuy, price, (amountOut * 1000 + 996) / 997);   // 0.3% fee
                return new BigInteger[]{ (result[0] * 997 + 999) / 1000, result[1] };
            }
        }
        #endregion

        /// <summary>
        /// Accept NEP-17 token
        /// SwapTokenInForTokenOut
        /// </summary>
        /// <param name="from"></param>
        /// <param name="amount"></param>
        /// <param name="data"></param>
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {

        }

        /// <summary>
        /// Calculate amountIn to reach AMM price
        /// </summary>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="quoteScale"></param>
        /// <param name="reserveIn"></param>
        /// <param name="reserveOut"></param>
        private static BigInteger GetAmountInTillPrice(bool isBuy, BigInteger price, BigInteger quoteScale, BigInteger reserveIn, BigInteger reserveOut)
        {
            Assert(price > 0 && quoteScale > 0 && reserveIn > 0 && reserveOut > 0, "Parameter Invalid");
            var amountIn = BigInteger.Pow(reserveIn, 2) * 9000000;
            if (isBuy) amountIn += reserveIn * reserveOut * price * 3988000000000 / quoteScale;
            else amountIn += reserveIn * reserveOut * quoteScale * 3988000000000 / price;
            return (amountIn.Sqrt() - reserveIn * 1997000) / 1994000;
        }

        private static BigInteger GetAmountInTillPriceWithFundFee(bool isBuy, BigInteger price, BigInteger quoteScale, BigInteger reserveIn, BigInteger reserveOut)
        {
            Assert(price > 0 && quoteScale > 0 && reserveIn > 0 && reserveOut > 0, "Parameter Invalid");
            var amountIn = BigInteger.Pow(reserveIn, 2) * 6250000;
            if (isBuy) amountIn += reserveIn * reserveOut * price * 3986006000000 / quoteScale;
            else amountIn += reserveIn * reserveOut * quoteScale * 3986006000000 / price;
            return (amountIn.Sqrt() - reserveIn * 1996500) / 1993003;
        }

        /// <summary>
        /// AMM GetAmountOut
        /// </summary>
        /// <param name="amountIn"></param>
        /// <param name="reserveIn"></param>
        /// <param name="reserveOut"></param>
        /// <returns></returns>
        private static BigInteger GetAmountOut(BigInteger amountIn, BigInteger reserveIn, BigInteger reserveOut)
        {
            Assert(amountIn >= 0 && reserveIn > 0 && reserveOut > 0, "AmountIn Must >= 0");
            var amountInWithFee = amountIn * 997;
            var numerator = amountInWithFee * reserveOut;
            var denominator = reserveIn * 1000 + amountInWithFee;
            var amountOut = numerator / denominator;
            return amountOut;
        }

        /// <summary>
        /// AMM GetAmountIn
        /// </summary>
        /// <param name="amountOut"></param>
        /// <param name="reserveIn"></param>
        /// <param name="reserveOut"></param>
        /// <returns></returns>
        private static BigInteger GetAmountIn(BigInteger amountOut, BigInteger reserveIn, BigInteger reserveOut)
        {
            Assert(amountOut >= 0 && reserveIn > 0 && reserveOut > 0, "AmountOut Must >= 0");
            var numerator = reserveIn * amountOut * 1000;
            var denominator = (reserveOut - amountOut) * 997;
            var amountIn = (numerator / denominator) + 1;
            return amountIn;
        }

        /// <summary>
        /// Swap as a router
        /// </summary>
        /// <param name="pairContract"></param>
        /// <param name="sender"></param>
        /// <param name="tokenIn"></param>
        /// <param name="tokenOut"></param>
        /// <param name="amountIn"></param>
        /// <param name="amountOut"></param>
        private static void SwapAMM(UInt160 pairContract, UInt160 sender, UInt160 tokenIn, UInt160 tokenOut, BigInteger amountIn, BigInteger amountOut)
        {
            SafeTransfer(tokenIn, sender, pairContract, amountIn);

            BigInteger amount0Out = 0;
            BigInteger amount1Out = 0;
            if (tokenIn.ToUInteger() < tokenOut.ToUInteger()) amount1Out = amountOut;
            else amount0Out = amountOut;

            SwapOut(pairContract, amount0Out, amount1Out, sender);
        }
    }
}
