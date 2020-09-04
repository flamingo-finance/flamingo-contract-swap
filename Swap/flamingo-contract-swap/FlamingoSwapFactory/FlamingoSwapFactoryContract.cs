using System;
using System.ComponentModel;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace FlamingoSwapFactory
{
    class FlamingoSwapFactoryContract : SmartContract
    {

        static readonly byte[] superAdmin = "AZaCs7GwthGy9fku2nFXtbrdKBRmrUQoFP".ToScriptHash();

        /// <summary>
        /// 收益地址StoreKey
        /// </summary>
        private const string FeeToKey = "FeeTo";


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



        //public delegate void deleTest(byte[] v);
        //[DisplayName("test")]
        //public static event deleTest onTest;
        //public delegate void deleTest2(BigInteger v);
        //[DisplayName("test2")]
        //public static event deleTest2 onTest2;
        //public delegate void deleSetExchangeFee(byte[] tokenHash, byte[] assetHash, BigInteger ratio);
        //[DisplayName("setExchangeFee")]
        //public static event deleSetExchangeFee onSetExchangeFee;
        #endregion

        public static object Main(string method, object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                return false;
            }
            if (Runtime.Trigger == TriggerType.Application)
            {
                byte[] tokenHash = (byte[])args[0];
                byte[] assetHash = (byte[])args[1];
                if (method == "createExchange")
                {
                    byte[] exchangeContractHash = (byte[])args[2];
                    return CreateExchangePair(tokenHash, assetHash, exchangeContractHash);
                }
                if (method == "removeExchange")
                {
                    return RemoveExchangePair(tokenHash, assetHash);
                }
                if (method == "getExchange")
                {
                    return GetExchange(tokenHash, assetHash);
                }
                if (method == "setFeeTo")
                {
                    return SetFeeTo((byte[])args[0]);
                }
                if (method == "getFeeTo")
                {
                    return GetFeeTo();
                }
                //转发
                //{
                //    StorageMap exchangeMap = Storage.CurrentContext.CreateMap("exchange");
                //    byte[] exchangeContractHash = exchangeMap.Get(tokenHash.Concat(assetHash));
                //    if (exchangeContractHash.Length == 0)
                //        throw new InvalidOperationException("exchangeContractHash inexistence");
                //    deleDyncall _dyncall = (deleDyncall)exchangeContractHash.ToDelegate();
                //    return _dyncall(operation, _args);
                //}
            }
            return false;
        }



        /// <summary>
        /// 增加nep5资产的exchange合约映射
        /// </summary>
        /// <param name="tokenA">Nep5 token</param>
        /// <param name="tokenB">Nep5 token</param>
        /// <param name="exchangeContractHash"></param>
        /// <returns></returns>
        public static bool CreateExchangePair(byte[] tokenA, byte[] tokenB, byte[] exchangeContractHash)
        {
            if (!Runtime.CheckWitness(superAdmin))
                throw new InvalidOperationException("Forbidden");
            if (tokenA == tokenB)
                throw new InvalidOperationException("Identical Addresses");
            var pair = GetTokenPair(tokenA, tokenB);
            if (pair.Token0.AsBigInteger() == 0)
                throw new InvalidOperationException("Zero Address");
            StorageMap exchangeMap = Storage.CurrentContext.CreateMap("exchange");

            var key = pair.Token0.Concat(pair.Token1);
            if (exchangeMap.Get(key).Length != 0)
                throw new InvalidOperationException("Exchange had created");

            exchangeMap.Put(key, exchangeContractHash);
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
            if (!Runtime.CheckWitness(superAdmin))
                throw new InvalidOperationException("Forbidden");
            StorageMap exchangeMap = Storage.CurrentContext.CreateMap("exchange");
            var pair = GetTokenPair(tokenA, tokenB);
            var key = pair.Token0.Concat(pair.Token1);
            if (exchangeMap.Get(key).Length == 0)
                throw new InvalidOperationException("exchange do not exit");
            exchangeMap.Delete(key);

            onRemoveExchange(tokenA, tokenB);
            return true;
        }




        /// <summary>
        /// 获得nep5资产的exchange合约映射
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        public static byte[] GetExchange(byte[] tokenA, byte[] tokenB)
        {
            var pair = GetTokenPair(tokenA, tokenB);
            StorageMap exchangeMap = Storage.CurrentContext.CreateMap("exchange");
            return exchangeMap.Get(pair.Token0.Concat(pair.Token1));
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
            if (feeTo.Length != 20)
            {
                throw new Exception("feeTo is not address");
            }
            if (!Runtime.CheckWitness(superAdmin))
            {
                throw new Exception("FORBIDDEN");
            }
            Storage.Put(FeeToKey, feeTo);
            return true;
        }

        private static TokenPair GetTokenPair(byte[] tokenA, byte[] tokenB)
        {
            return tokenA.AsBigInteger() < tokenB.AsBigInteger()
                ? new TokenPair { Token0 = tokenA, Token1 = tokenB }
                : new TokenPair { Token0 = tokenB, Token1 = tokenA };
        }
    }


    struct TokenPair
    {
        public byte[] Token0;
        public byte[] Token1;
    }
}

