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
        public struct LimitOrder
        {
            public UInt160 maker;
            public BigInteger price;
            public BigInteger amount;
            public ByteString nextID;
        }

        public struct OrderReceipt
        {
            public UInt160 baseToken;
            public UInt160 quoteToken;
            public ByteString id;
            public ulong time;
            public bool isBuy;
            public UInt160 maker;
            public BigInteger price;
            public BigInteger totalAmount;
            public BigInteger leftAmount;
        }

        public struct OrderBook
        {
            public UInt160 baseToken;
            public UInt160 quoteToken;
            public BigInteger quoteScale;
            public BigInteger minOrderAmount;
            public BigInteger maxOrderAmount;

            public ByteString firstBuyID;
            public ByteString firstSellID;
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
            if (BookExists(pairKey)) return false;
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
            Assert(baseToken.IsAddress() && quoteToken.IsAddress(), "Invalid Address");
            Assert(minOrderAmount > 0, "Invalid Amount Limit");
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            if (!BookExists(pairKey)) return false;
            var book = GetOrderBook(pairKey);
            if (book.baseToken != baseToken || book.quoteToken != quoteToken) return false;

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
            Assert(baseToken.IsAddress() && quoteToken.IsAddress(), "Invalid Address");
            Assert(maxOrderAmount > 0, "Invalid Amount Limit");
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            if (!BookExists(pairKey)) return false;
            var book = GetOrderBook(pairKey);
            if (book.baseToken != baseToken || book.quoteToken != quoteToken) return false;

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
            Assert(baseToken.IsAddress() && quoteToken.IsAddress(), "Invalid Address");
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            if (!BookExists(pairKey) || BookPaused(pairKey)) return false;
            var book = GetOrderBook(pairKey);
            if (book.baseToken != baseToken || book.quoteToken != quoteToken) return false;

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
            Assert(baseToken.IsAddress() && quoteToken.IsAddress(), "Invalid Address");
            Assert(Verify(), "No Authorization");

            var pairKey = GetPairKey(baseToken, quoteToken);
            if (!BookExists(pairKey) || !BookPaused(pairKey)) return false;
            var book = GetOrderBook(pairKey);
            if (book.baseToken != baseToken || book.quoteToken != quoteToken) return false;

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
            return AddLimitOrder(tokenA, tokenB, maker, isBuy, price, amount, amount);
        }

        public static ByteString AddLimitOrder(UInt160 tokenA, UInt160 tokenB, UInt160 maker, bool isBuy, BigInteger price, BigInteger amount, BigInteger receiptAmount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(!BookPaused(pairKey), "Book is Paused");

            // Check parameters
            Assert(price > 0 && amount > 0 && receiptAmount >= amount, "Invalid Parameters");
            if (receiptAmount > amount) Assert(CheckIsRouter(Runtime.CallingScriptHash), "Only Router Can Custom Receipt");

            // Check maker
            Assert(Runtime.CheckWitness(maker), "No Authorization");
            Assert(ContractManagement.GetContract(maker) == null, "Forbidden");

            // Deal as market order
            var bookInfo = GetOrderBook(pairKey);
            var leftAmount = DealMarketOrderInternal(pairKey, maker, isBuy, price, amount);
            if (leftAmount == 0) return null;
            if (leftAmount < bookInfo.minOrderAmount || leftAmount > bookInfo.maxOrderAmount) return null;

            // Deposit token
            var me = Runtime.ExecutingScriptHash;
            if (isBuy) SafeTransfer(bookInfo.quoteToken, maker, me, leftAmount * price / bookInfo.quoteScale);
            else SafeTransfer(bookInfo.baseToken, maker, me, leftAmount);

            // Do add
            var id = GetUnusedID();
            Assert(InsertOrder(pairKey, id, new LimitOrder(){
                maker = maker,
                price = price,
                amount = leftAmount
            }, isBuy), "Add Order Fail");

            // Add receipt
            SetReceipt(maker, id, new OrderReceipt(){
                baseToken = bookInfo.baseToken,
                quoteToken = bookInfo.quoteToken,
                id = id,
                time = Runtime.Time,
                isBuy = isBuy,
                totalAmount = receiptAmount
            });
            onOrderStatusChanged(bookInfo.baseToken, bookInfo.quoteToken, id, !!isBuy, maker, price, leftAmount);
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
            return AddLimitOrderAt(parentID, maker, price, amount, amount);
        }

        public static ByteString AddLimitOrderAt(ByteString parentID, UInt160 maker, BigInteger price, BigInteger amount, BigInteger receiptAmount)
        {
            Assert(OrderExists(parentID), "Parent Order Not Exists");
            var receipt = GetReceipt(GetOrder(parentID).maker, parentID);

            // Check if paused
            var pairKey = GetPairKey(receipt.baseToken, receipt.quoteToken);
            Assert(!BookPaused(pairKey), "Book is Paused");

            // Check parameters
            Assert(price > 0 && amount > 0 && receiptAmount >= amount, "Invalid Parameters");
            if (receiptAmount > amount) Assert(CheckIsRouter(Runtime.CallingScriptHash), "Only Router Can Custom Receipt");

            // Check maker
            Assert(Runtime.CheckWitness(maker), "No Authorization");
            Assert(ContractManagement.GetContract(maker) == null, "Forbidden");

            // Check amount
            var bookInfo = GetOrderBook(pairKey);
            Assert(amount >= bookInfo.minOrderAmount && amount <= bookInfo.maxOrderAmount, "Invalid Limit Order Amount");

            // Deposit token
            var me = Runtime.ExecutingScriptHash;
            if (receipt.isBuy) SafeTransfer(receipt.quoteToken, maker, me, amount * price / bookInfo.quoteScale);
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
                totalAmount = receiptAmount
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
            Assert(BookExists(pairKey), "Book Not Exists");
            if (!OrderExists(id)) return false;
            var order = GetOrder(id);
            Assert(Runtime.CheckWitness(order.maker), "No Authorization");

            // Do remove
            var bookInfo = GetOrderBook(pairKey);
            Assert(RemoveOrder(pairKey, id, isBuy), "Remove Order Fail");

            // Remove receipt
            DeleteReceipt(order.maker, id);
            onOrderStatusChanged(bookInfo.baseToken, bookInfo.quoteToken, id, !!isBuy, order.maker, order.price, 0);

            // Withdraw token
            var me = Runtime.ExecutingScriptHash;
            if (isBuy) SafeTransfer(bookInfo.quoteToken, me, order.maker, order.amount * order.price / bookInfo.quoteScale);
            else SafeTransfer(bookInfo.baseToken, me, order.maker, order.amount);
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
            if (!OrderExists(id)) return false;
            Assert(OrderExists(parentID), "Parent Order Not Exists");
            var order = GetOrder(id);
            Assert(Runtime.CheckWitness(order.maker), "No Authorization");

            // Do remove
            var receipt = GetReceipt(order.maker, id);
            var pairKey = GetPairKey(receipt.baseToken, receipt.quoteToken);
            var quoteScale = GetQuoteScale(pairKey);

            Assert(RemoveOrderAt(parentID, id), "Remove Order Fail");

            // Remove receipt
            DeleteReceipt(order.maker, id);
            onOrderStatusChanged(receipt.baseToken, receipt.quoteToken, id, !!receipt.isBuy, order.maker, order.price, 0);

            // Withdraw token
            var me = Runtime.ExecutingScriptHash;
            if (receipt.isBuy) SafeTransfer(receipt.quoteToken, me, order.maker, order.amount * order.price / quoteScale);
            else SafeTransfer(receipt.baseToken, me, order.maker, order.amount);
            return true;
        }

        /// <summary>
        /// Try get the parent order id of an existing order
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="parentID"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static ByteString GetParentOrderID(UInt160 tokenA, UInt160 tokenB, bool isBuy, ByteString id)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(OrderExists(id), "Order Not Exists");

            return GetParentID(pairKey, isBuy, id);
        }

        /// <summary>
        /// Get first N limit orders and their details
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public static OrderReceipt[] GetFirstNOrders(UInt160 tokenA, UInt160 tokenB, bool isBuy, uint n)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            var results = new OrderReceipt[n];
            var firstID = GetFirstOrderID(pairKey, isBuy);
            if (firstID is null) return results;

            var currentOrderID = firstID;
            var currentOrder = GetOrder(currentOrderID);
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
        /// Get all orders and their details of maker
        /// </summary>
        /// <param name="maker"></param>
        /// <returns></returns>
        [Safe]
        public static OrderReceipt[] GetOrdersOf(UInt160 maker)
        {
            // Get receipts
            var receipts = GetReceiptsOf(maker);
            // Makeup details
            for (int i = 0; i < receipts.Length; i++)
            {
                var order = GetOrder(receipts[i].id);
                receipts[i].maker = order.maker;
                receipts[i].price = order.price;
                receipts[i].leftAmount = order.amount;
            }
            return receipts;
        }

        /// <summary>
        /// Get the total reverse of tradable orders with an expected price
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <returns>Tradable base amount</returns>
        public static BigInteger GetTotalTradable(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0, "Invalid Price");
            if (BookPaused(pairKey)) return 0;

            return GetTotalTradableInternal(pairKey, isBuy, price);
        }

        private static BigInteger GetTotalTradableInternal(byte[] pairKey, bool isBuy, BigInteger price)
        {
            BigInteger totalTradable = 0;
            var firstID = GetFirstOrderID(pairKey, !isBuy);
            if (firstID is null) return totalTradable;

            var currentID = firstID;

            while (currentID is not null)
            {
                var currentOrder = GetOrder(currentID);
                // Check price
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
        /// <returns>Left amount and total payment</returns>
        public static BigInteger[] MatchOrder(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price, BigInteger amount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0 && amount > 0, "Invalid Parameters");

            var firstID = GetFirstOrderID(pairKey, !isBuy);
            if (BookPaused(pairKey) || firstID is null) return new BigInteger[] { amount, 0 };
            return MatchOrderInternal(pairKey, isBuy, firstID, price, amount);
        }

        private static BigInteger[] MatchOrderInternal(byte[] pairKey, bool isBuy, ByteString anchorID, BigInteger price, BigInteger amount)
        {
            BigInteger totalPayment = 0;
            var bookInfo = GetOrderBook(pairKey);
            var currentOrder = GetOrder(anchorID);
            if (anchorID != (isBuy ? bookInfo.firstSellID : bookInfo.firstBuyID))
            {
                var anchorReceipt = GetReceipt(currentOrder.maker, anchorID);
                Assert(isBuy == !anchorReceipt.isBuy && bookInfo.baseToken == anchorReceipt.baseToken && bookInfo.quoteToken == anchorReceipt.quoteToken, "Invalid Anchor");
            }

            while (amount > 0)
            {
                // Check price
                if ((isBuy && currentOrder.price > price) || (!isBuy && currentOrder.price < price)) break;

                if (currentOrder.amount <= amount) 
                {
                    totalPayment += currentOrder.amount * currentOrder.price / bookInfo.quoteScale;
                    amount -= currentOrder.amount;
                }
                else
                {
                    totalPayment += amount * currentOrder.price / bookInfo.quoteScale;
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
        public static BigInteger[] MatchQuote(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price, BigInteger quoteAmount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(price > 0 && quoteAmount > 0, "Invalid Parameters");

            var firstID = GetFirstOrderID(pairKey, !isBuy);
            if (BookPaused(pairKey) || firstID is null) return new BigInteger[] { quoteAmount, 0 };
            return MatchQuoteInternal(pairKey, isBuy, firstID, price, quoteAmount);
        }

        private static BigInteger[] MatchQuoteInternal(byte[] pairKey, bool isBuy, ByteString anchorID, BigInteger price, BigInteger quoteAmount)
        {
            BigInteger totalTradable = 0;
            var bookInfo = GetOrderBook(pairKey);
            var currentOrder = GetOrder(anchorID);
            if (anchorID != (isBuy ? bookInfo.firstSellID : bookInfo.firstBuyID))
            {
                var anchorReceipt = GetReceipt(currentOrder.maker, anchorID);
                Assert(isBuy == !anchorReceipt.isBuy && bookInfo.baseToken == anchorReceipt.baseToken && bookInfo.quoteToken == anchorReceipt.quoteToken, "Invalid Anchor");
            }

            while (quoteAmount > 0)
            {
                // Check price
                if ((isBuy && currentOrder.price > price) || (!isBuy && currentOrder.price < price)) break;
                var payment = currentOrder.amount * currentOrder.price / bookInfo.quoteScale;
                if (payment <= quoteAmount) 
                {
                    totalTradable += currentOrder.amount;
                    quoteAmount -= payment;
                }
                else
                {
                    // For buyer, real payment <= expected
                    if (isBuy) totalTradable += quoteAmount * bookInfo.quoteScale / currentOrder.price;
                    // For seller, real payment >= expected
                    else totalTradable += (quoteAmount * bookInfo.quoteScale + currentOrder.price - 1) / currentOrder.price;
                    quoteAmount = 0;
                }

                if (currentOrder.nextID is null) break;
                currentOrder = GetOrder(currentOrder.nextID);
            }
            return new BigInteger[] { quoteAmount, totalTradable };
        }

        /// <summary>
        /// Try to make a market deal with orderbook
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
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(!BookPaused(pairKey), "Book is Paused");

            // Check parameters
            Assert(price > 0 && amount > 0, "Invalid Parameters");

            // Check taker
            Assert(Runtime.CheckWitness(taker), "No Authorization");
            Assert(ContractManagement.GetContract(taker) == null, "Forbidden");

            return DealMarketOrderInternal(pairKey, taker, isBuy, price, amount);
        }

        public static BigInteger DealMarketOrder(UInt160 tokenA, UInt160 tokenB, bool isBuy, BigInteger price, BigInteger amount)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(!BookPaused(pairKey), "Book is Paused");

            // Check parameters
            Assert(price > 0 && amount > 0, "Invalid Parameters");

            // Check taker
            var caller = Runtime.CallingScriptHash;
            Assert(ContractManagement.GetContract(caller) != null, "Forbidden"); 

            return DealMarketOrderInternal(pairKey, caller, isBuy, price, amount);
        }


        private static BigInteger DealMarketOrderInternal(byte[] pairKey, UInt160 taker, bool isBuy, BigInteger price, BigInteger amount)
        {
            var firstID = GetFirstOrderID(pairKey, !isBuy);
            if (firstID is null) return amount;
            var firstOrder = GetOrder(firstID);
            var canDeal = (isBuy && firstOrder.price <= price) || (!isBuy && firstOrder.price >= price);
            if (!canDeal) return amount;

            // Charge before settlement
            var me = Runtime.ExecutingScriptHash;
            var matchResult = MatchOrderInternal(pairKey, isBuy, firstID, price, amount);

            if (ContractManagement.GetContract(taker) != null)
            {
                if (isBuy) RequestTransfer(GetQuoteToken(pairKey), taker, me, matchResult[1]);
                else RequestTransfer(GetBaseToken(pairKey), taker, me, amount - matchResult[0]);
            }
            else
            {
                if (isBuy) SafeTransfer(GetQuoteToken(pairKey), taker, me, matchResult[1]);
                else SafeTransfer(GetBaseToken(pairKey), taker, me, amount - matchResult[0]);
            }

            return ExecuteDeal(pairKey, taker, isBuy, price, amount);
        }

        /// <summary>
        /// Settle order based on expected price
        /// </summary>
        /// <param name="pairKey"></param>
        /// <param name="taker"></param>
        /// <param name="isBuy"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <returns>Left amount</returns>
        private static BigInteger ExecuteDeal(byte[] pairKey, UInt160 taker, bool isBuy, BigInteger price, BigInteger amount)
        {
            var leftAmount = amount;
            var me = Runtime.ExecutingScriptHash;
            var bookInfo = GetOrderBook(pairKey);
            var fundAddress = GetFundAddress();
            var firstID = isBuy ? bookInfo.firstSellID : bookInfo.firstBuyID;

            BigInteger quoteFee = 0;
            BigInteger baseFee = 0;

            var totalQuotePayment = new Map<UInt160, BigInteger>();
            var totalBasePayment = new Map<UInt160, BigInteger>();

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
                    quoteAmount = currentOrder.amount * currentOrder.price / bookInfo.quoteScale;
                    baseAmount = currentOrder.amount;

                    // Remove full-fill order
                    DeleteOrder(currentID);
                    DeleteReceipt(currentOrder.maker, currentID);
                    firstID = currentOrder.nextID;

                    onOrderStatusChanged(bookInfo.baseToken, bookInfo.quoteToken, currentID, !isBuy, currentOrder.maker, currentOrder.price, 0);
                    leftAmount -= currentOrder.amount;
                }
                else
                {
                    // Part-fill
                    quoteAmount = leftAmount * currentOrder.price / bookInfo.quoteScale;
                    baseAmount = leftAmount;

                    // Update order
                    currentOrder.amount -= leftAmount;
                    SetOrder(currentID, currentOrder);

                    onOrderStatusChanged(bookInfo.baseToken, bookInfo.quoteToken, currentID, !isBuy, currentOrder.maker, currentOrder.price, currentOrder.amount);
                    leftAmount = 0;
                }

                // Record payment
                quotePayment = quoteAmount * 997 / 1000;
                basePayment = baseAmount * 997 / 1000;
                quoteFee += quoteAmount - quotePayment;
                baseFee += baseAmount - basePayment;

                if (isBuy)
                {
                    if (totalQuotePayment.HasKey(currentOrder.maker)) totalQuotePayment[currentOrder.maker] += quotePayment;
                    else totalQuotePayment[currentOrder.maker] = quotePayment;
                    if (totalBasePayment.HasKey(taker)) totalBasePayment[taker] += basePayment;
                    else totalBasePayment[taker] = basePayment;
                }
                else
                {
                    if (totalQuotePayment.HasKey(taker)) totalQuotePayment[taker] += quotePayment;
                    else totalQuotePayment[taker] = quotePayment;
                    if (totalBasePayment.HasKey(currentOrder.maker)) totalBasePayment[currentOrder.maker] += basePayment;
                    else totalBasePayment[currentOrder.maker] = basePayment;
                }

                // Check if still tradable
                if (leftAmount == 0) break;
                currentID = currentOrder.nextID;
            }

            // Update book if necessary
            if (isBuy && bookInfo.firstSellID != firstID)
            {
                bookInfo.firstSellID = firstID;
                SetOrderBook(pairKey, bookInfo);
            }
            if (!isBuy && bookInfo.firstBuyID != firstID)
            {
                bookInfo.firstBuyID = firstID;
                SetOrderBook(pairKey, bookInfo);
            }

            // Do transfer
            foreach (var toAddress in totalQuotePayment.Keys)
            {
                if (totalQuotePayment[toAddress] > 0) SafeTransfer(bookInfo.quoteToken, me, toAddress, totalQuotePayment[toAddress]);
            }
            foreach (var toAddress in totalBasePayment.Keys)
            {
                if (totalBasePayment[toAddress] > 0) SafeTransfer(bookInfo.baseToken, me, toAddress, totalBasePayment[toAddress]);
            }

            if (fundAddress is not null)
            {
                if (quoteFee > 0) SafeTransfer(bookInfo.quoteToken, me, fundAddress, quoteFee);
                if (baseFee > 0) SafeTransfer(bookInfo.baseToken, me, fundAddress, baseFee);
            }
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
            if (!OrderExists(id)) return false;
            Assert(OrderExists(parentID), "Parent Order Not Exists");
            var order = GetOrder(id);
            Assert(Runtime.CheckWitness(taker), "No Authorization");

            // Do deal
            var me = Runtime.ExecutingScriptHash;
            var receipt = GetReceipt(order.maker, id);
            var pairKey = GetPairKey(receipt.baseToken, receipt.quoteToken);
            var quoteScale = GetQuoteScale(pairKey);
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
                if (basePayment > 0) SafeTransfer(receipt.baseToken, me, order.maker, basePayment);
                if (quotePayment > 0) SafeTransfer(receipt.quoteToken, me, taker, quotePayment);
            }
            else
            {
                if (basePayment > 0) SafeTransfer(receipt.baseToken, me, taker, basePayment);
                if (quotePayment > 0) SafeTransfer(receipt.quoteToken, me, order.maker, quotePayment);
            }

            if (fundAddress is not null)
            {
                if (baseFee > 0) SafeTransfer(receipt.baseToken, me, fundAddress, baseFee);
                if (quoteFee > 0) SafeTransfer(receipt.quoteToken, me, fundAddress, quoteFee);
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
        public static (ByteString, BigInteger) GetMarketPrice(UInt160 tokenA, UInt160 tokenB, bool isBuy)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            var firstID = GetFirstOrderID(pairKey, !isBuy);
            if (firstID is null) return (firstID, 0);

            return (firstID, GetOrder(firstID).price);
        }

        /// <summary>
        /// Get the next price to perform match
        /// </summary>
        /// <param name="anchorID"></param>
        /// <returns></returns>
        public static (ByteString, BigInteger) GetNextPrice(ByteString anchorID)
        {
            // Check if exist
            Assert(OrderExists(anchorID), "Anchor Order Not Exists");

            var currentID = anchorID;
            var currentOrder = GetOrder(currentID);
            var price = currentOrder.price;

            while (currentOrder.price == price)
            {
                currentID = currentOrder.nextID;
                if (currentID is null) return (currentID, 0);
                currentOrder = GetOrder(currentID);
            }
            return (currentID, currentOrder.price);
        }

        /// <summary>
        /// Get book detail
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        public static bool BookTradable(UInt160 tokenA, UInt160 tokenB)
        {
            var pairKey = GetPairKey(tokenA, tokenB);
            return BookExists(pairKey) && !BookPaused(pairKey);
        }

        public static UInt160 GetBaseToken(UInt160 tokenA, UInt160 tokenB)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            return GetBaseToken(pairKey);
        }

        public static UInt160 GetQuoteToken(UInt160 tokenA, UInt160 tokenB)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            return GetQuoteToken(pairKey);
        }

        public static BigInteger GetQuoteScale(UInt160 tokenA, UInt160 tokenB)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            return GetQuoteScale(pairKey);
        }

        public static BigInteger GetMaxOrderAmount(UInt160 tokenA, UInt160 tokenB)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");

            return GetMaxOrderAmount(pairKey);
        }

        public static BigInteger GetMinOrderAmount(UInt160 tokenA, UInt160 tokenB)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenA, tokenB);
            Assert(BookExists(pairKey), "Book Not Exists");
            
            return GetMinOrderAmount(pairKey);
        }
        #endregion

        #region AMM like API
        /// <summary>
        /// Calculate amountOut with amountIn
        /// </summary>
        /// <param name="tokenFrom"></param>
        /// <param name="tokenTo"></param>
        /// <param name="anchorID"></param>
        /// <param name="price"></param>
        /// <param name="amountIn"></param>
        /// <returns>Unsatisfied amountIn and amountOut</returns>
        public static BigInteger[] GetAmountOut(UInt160 tokenFrom, UInt160 tokenTo, ByteString anchorID, BigInteger price, BigInteger amountIn)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(OrderExists(anchorID), "Anchor Order Not Exists");
            Assert(price > 0 && amountIn > 0, "Invalid Parameters");

            var isBuy = tokenFrom == GetQuoteToken(pairKey);
            if (isBuy)
            {
                var result = MatchQuoteInternal(pairKey, isBuy, anchorID, price, amountIn);
                return new BigInteger[]{ result[0], result[1] * 997 / 1000 };   // 0.3% fee
            }
            else
            {
                var result = MatchOrderInternal(pairKey, isBuy, anchorID, price, amountIn);
                return new BigInteger[]{ result[0], result[1] * 997 / 1000 };   // 0.3% fee
            }
        }

        /// <summary>
        /// Calculate amountOut with amountIn
        /// </summary>
        /// <param name="tokenFrom"></param>
        /// <param name="tokenTo"></param>
        /// <param name="anchorID"></param>
        /// <param name="price"></param>
        /// <param name="amountOut"></param>
        /// <returns>Unsatisfied amountOut and amountIn</returns>
        public static BigInteger[] GetAmountIn(UInt160 tokenFrom, UInt160 tokenTo, ByteString anchorID, BigInteger price, BigInteger amountOut)
        {
            // Check if exist
            var pairKey = GetPairKey(tokenFrom, tokenTo);
            Assert(BookExists(pairKey), "Book Not Exists");
            Assert(OrderExists(anchorID), "Anchor Order Not Exists");
            Assert(price > 0 && amountOut > 0, "Invalid Parameters");

            var isBuy = tokenFrom == GetQuoteToken(pairKey);
            if (isBuy)
            {
                var result = MatchOrderInternal(pairKey, isBuy, anchorID, price, (amountOut * 1000 + 996) / 997);   // 0.3% fee
                return new BigInteger[]{ result[0] * 997 / 1000, result[1] };
            }
            else
            {
                var result = MatchQuoteInternal(pairKey, isBuy, anchorID, price, (amountOut * 1000 + 996) / 997);   // 0.3% fee
                return new BigInteger[]{ (result[0] * 997 + 999) / 1000, result[1] };
            }
        }
        #endregion

        public static void OnNEP17Payment(UInt160 sender, BigInteger amountIn, object data)
        {

        }
    }
}
