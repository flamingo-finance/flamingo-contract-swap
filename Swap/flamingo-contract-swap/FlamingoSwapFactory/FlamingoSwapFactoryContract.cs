using System;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace FlamingoSwapFactory
{
    partial class FlamingoSwapFactoryContract : SmartContract
    {

        /// <summary>
        /// 收益地址的StoreKey
        /// </summary>
        private const string FeeToKey = "FeeTo";

        /// <summary>
        /// 交易对Map的StoreKey
        /// </summary>
        private const string ExchangeMapKey = "ExchangeMap";


        #region 通知

        /// <summary>
        /// params: tokenA,tokenB,exchangeContractHash
        /// </summary>
        [DisplayName("createExchange")]
        public static event Action<byte[], byte[], byte[]> onCreateExchange;

        /// <summary>
        /// params: tokenA,tokenB
        /// </summary>
        [DisplayName("removeExchange")]
        public static event Action<byte[], byte[]> onRemoveExchange;


        #endregion

        public static object Main(string method, object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(GetAdmin());
            }
            if (Runtime.Trigger == TriggerType.Application)
            {
                if (method == "getExchangePair")
                {
                    //var tokenA = (byte[])args[0];
                    //var tokenB = (byte[])args[1];
                    //return Storage.Get(tokenA.AsBigInteger() < tokenB.AsBigInteger()
                    //    ? ExchangeMapKey.AsByteArray().Concat(tokenA).Concat(tokenB)
                    //    : ExchangeMapKey.AsByteArray().Concat(tokenB).Concat(tokenA));//0.182
                    return Storage.Get(GetPairKey((byte[])args[0], (byte[])args[1]));//优化gas,0.199
                    //return GetExchangePair((byte[])args[0], (byte[])args[1]);
                }
                if (method == "getAllExchangePair")
                {
                    return GetAllExchangePair();
                }
                if (method == "createExchangePair")
                {
                    return CreateExchangePair((byte[])args[0], (byte[])args[1], (byte[])args[2]);
                }
                if (method == "removeExchangePair")
                {
                    return RemoveExchangePair((byte[])args[0], (byte[])args[1]);
                }
                if (method == "getFeeTo")
                {
                    return GetFeeTo();
                }
                if (method == "setFeeTo")
                {
                    return SetFeeTo((byte[])args[0]);
                }

                if (method == "getAdmin")
                {
                    return GetAdmin();
                }

                if (method == "setAdmin")
                {
                    return SetAdmin((byte[])args[0]);
                }

                if (method == "upgrade")
                {
                    Assert(args.Length == 9, "upgrade: args.Length != 9.");
                    byte[] script = (byte[])args[0];
                    byte[] plist = (byte[])args[1];
                    byte rtype = (byte)args[2];
                    ContractPropertyState cps = (ContractPropertyState)args[3];
                    string name = (string)args[4];
                    string version = (string)args[5];
                    string author = (string)args[6];
                    string email = (string)args[7];
                    string description = (string)args[8];
                    return Upgrade(script, plist, rtype, cps, name, version, author, email, description);
                }
            }
            return false;
        }



        /// <summary>
        /// 增加nep5资产的exchange合约映射
        /// </summary>
        /// <param name="tokenA">Nep5 tokenA</param>
        /// <param name="tokenB">Nep5 tokenB</param>
        /// <param name="exchangeContractHash"></param>
        /// <returns></returns>
        public static bool CreateExchangePair(byte[] tokenA, byte[] tokenB, byte[] exchangeContractHash)
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            Assert(tokenA != tokenB, "Identical Address", tokenA);
            AssertAddress(tokenA, nameof(tokenA));
            AssertAddress(tokenB, nameof(tokenB));
            AssertAddress(exchangeContractHash, nameof(exchangeContractHash));

            //var pair = GetTokenPair(tokenA, tokenB);
            // ExchangeMapKey.AsByteArray().Concat(pair.Token0).Concat(pair.Token1);
            var key = GetPairKey(tokenA, tokenB);
            var value = Storage.Get(key);
            Assert(value.Length == 0, "Exchange had created");

            Storage.Put(key, exchangeContractHash);
            onCreateExchange(tokenA, tokenB, exchangeContractHash);
            return true;
        }

        /// <summary>
        /// 删除nep5资产的exchange合约映射
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        public static bool RemoveExchangePair(byte[] tokenA, byte[] tokenB)
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            AssertAddress(tokenA, nameof(tokenA));
            AssertAddress(tokenB, nameof(tokenB));

            var key = GetPairKey(tokenA, tokenB);
            var value = Storage.Get(key);
            if (value.Length > 0)
            {
                Storage.Delete(key);
                onRemoveExchange(tokenA, tokenB);
            }
            return true;
        }




        ///// <summary>
        ///// 获得nep5资产的exchange合约映射
        ///// </summary>
        ///// <param name="tokenA"></param>
        ///// <param name="tokenB"></param>
        ///// <returns></returns>
        //public static byte[] GetExchangePair(byte[] tokenA, byte[] tokenB)
        //{
        //    return Storage.Get(GetPairKey(tokenA, tokenB));
        //}


        /// <summary>
        /// 获得nep5资产的exchange合约映射
        /// </summary>
        /// <returns></returns>
        public static ExchangePair[] GetAllExchangePair()
        {
            var iterator = Storage.Find(ExchangeMapKey);
            var result = new ExchangePair[0];
            while (iterator.Next())
            {
                var exchangeContractHash = iterator.Value;
                if (exchangeContractHash.Length == 20)
                {
                    var keyPair = iterator.Key.AsByteArray().Last(40);
                    var tokenA = keyPair.Take(20);
                    var tokenB = keyPair.Last(20);
                    var item = new ExchangePair()
                    {
                        TokenA = tokenA,
                        TokenB = tokenB,
                        ExchangePairHash = exchangeContractHash,
                    };
                    Append(result, item);
                }
            }
            return result;
        }

        /// <summary>
        /// 获取手续费收益地址
        /// </summary>
        /// <returns></returns>
        private static byte[] GetFeeTo()
        {
            return Storage.Get(FeeToKey);
        }


        /// <summary>
        /// 设置手续费收益地址
        /// </summary>
        /// <param name="feeTo"></param>
        /// <returns></returns>
        private static bool SetFeeTo(byte[] feeTo)
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            Storage.Put(FeeToKey, feeTo);
            return true;
        }

      

        /// <summary>
        /// 获取pair
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        private static byte[] GetPairKey(byte[] tokenA, byte[] tokenB)
        {
            return tokenA.AsBigInteger() < tokenB.AsBigInteger()
                ? ExchangeMapKey.AsByteArray().Concat(tokenA).Concat(tokenB)
                : ExchangeMapKey.AsByteArray().Concat(tokenB).Concat(tokenA);
        }
    }
}

