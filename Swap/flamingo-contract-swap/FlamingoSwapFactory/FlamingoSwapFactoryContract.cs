using System.ComponentModel;
using FlamingoSwapFactory.Models;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;

namespace FlamingoSwapFactory
{
    [DisplayName("Flamingo Swap Factory Contract")]
    [ManifestExtra("Author", "Flamingo Finance")]
    [ManifestExtra("Email", "developer@flamingo.finance")]
    [ManifestExtra("Description", "This is a Flamingo Contract")]
    [ContractPermission("*")]//avoid native contract hash change
    public partial class FlamingoSwapFactoryContract : SmartContract
    {
        /// <summary>
        /// 交易对列表的存储区前缀，只允许一字节
        /// </summary>
        private static readonly byte[] ExchangeMapKey = new byte[] { 0xff };

        /// <summary>
        /// 查询交易对合约,ByteString 可以为null，交给调用端判断
        /// </summary>
        /// <param name="tokenA">Nep5 tokenA</param>
        /// <param name="tokenB">Nep5 tokenB</param>
        /// <returns></returns>
        public static ByteString GetExchangePair(UInt160 tokenA, UInt160 tokenB)
        {
            Assert(tokenA != tokenB, "Identical Address", tokenA);
            return StorageGet(GetPairKey(tokenA, tokenB));
        }

        /// <summary>
        /// 增加nep17资产的exchange合约映射
        /// </summary>
        /// <param name="tokenA">Nep5 tokenA</param>
        /// <param name="tokenB">Nep5 tokenB</param>
        /// <param name="exchangeContractHash"></param>
        /// <returns></returns>
        public static bool CreateExchangePair(UInt160 tokenA, UInt160 tokenB, UInt160 exchangeContractHash)
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            Assert(tokenA.IsAddress() && tokenB.IsAddress(), "Invalid Address");
            Assert(tokenA != tokenB, "Identical Address", tokenA);
            var key = GetPairKey(tokenA, tokenB);
            var value = StorageGet(key);
            Assert(value == null || value.Length == 0, "Exchange Already Existed");

            StoragePut(key, exchangeContractHash);
            onCreateExchange(tokenA, tokenB, exchangeContractHash);
            return true;
        }


        /// <summary>
        /// 增加nep17资产的exchange合约映射
        /// </summary>
        /// <param name="exchangeContractHash"></param>
        /// <returns></returns>
        public static bool RegisterExchangePair(UInt160 exchangeContractHash)
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            var contract = ContractManagement.GetContract(exchangeContractHash);
            Assert(contract != null, "Not Deployed");
            var token0 = (UInt160)Contract.Call(exchangeContractHash, "getToken0", CallFlags.ReadOnly, new object[0]);
            var token1 = (UInt160)Contract.Call(exchangeContractHash, "getToken1", CallFlags.ReadOnly, new object[0]);
            Assert(token0 != null && token1 != null, "Token Invalid");
            var key = GetPairKey(token0, token1);
            //var value = StorageGet(key);
            //if (value != null)
            //{
            //    onRemoveExchange(token0, token1);
            //}
            StoragePut(key, exchangeContractHash);
            onCreateExchange(token0, token1, exchangeContractHash);
            return true;
        }

        /// <summary>
        /// 删除nep17资产的exchange合约映射
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        public static bool RemoveExchangePair(UInt160 tokenA, UInt160 tokenB)
        {
            Assert(tokenA.IsAddress() && tokenB.IsAddress(), "Invalid Address");
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            var key = GetPairKey(tokenA, tokenB);
            var value = StorageGet(key);
            if (value?.Length > 0)
            {
                StorageDelete(key);
                onRemoveExchange(tokenA, tokenB);
            }
            return true;
        }



        /// <summary>
        /// 获得nep17资产的exchange合约映射
        /// </summary>
        /// <returns></returns>
        public static ExchangePair[] GetAllExchangePair()
        {
            var iterator = (Iterator<KeyValue>)StorageFind(ExchangeMapKey);
            var result = new ExchangePair[0];
            while (iterator.Next())
            {
                var keyValue = iterator.Value;
                if (keyValue.Value != null)
                {
                    var exchangeContractHash = keyValue.Value;
                    var tokenA = keyValue.Key.Take(20);
                    var tokenB = keyValue.Key.Last(20);
                    var item = new ExchangePair()
                    {
                        TokenA = (UInt160)tokenA,
                        TokenB = (UInt160)tokenB,
                        ExchangePairHash = exchangeContractHash,
                    };
                    Append(result, item);
                }
            }
            return result;
        }




        /// <summary>
        /// 获取pair
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        private static byte[] GetPairKey(UInt160 tokenA, UInt160 tokenB)
        {
            return tokenA.ToUInteger() < tokenB.ToUInteger()
                ? ExchangeMapKey.Concat(tokenA).Concat(tokenB)
                : ExchangeMapKey.Concat(tokenB).Concat(tokenA);
        }
    }
}

