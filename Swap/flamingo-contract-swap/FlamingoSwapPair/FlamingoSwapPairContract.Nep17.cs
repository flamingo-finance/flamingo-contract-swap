using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;

namespace FlamingoSwapPair
{
    partial class FlamingoSwapPairContract
    {
        public static ulong Decimals() => 8;
        public static BigInteger TotalSupply() => TotalSupplyStorage.Get();

        public static BigInteger BalanceOf(UInt160 account) => AssetStorage.Get(account);

        public static bool Transfer(UInt160 from, UInt160 to, BigInteger amount, object data)
        {
            if (amount <= 0) throw new Exception("The parameter amount MUST be greater than 0.");
            if (!Runtime.CheckWitness(from) && !from.Equals(ExecutionEngine.CallingScriptHash)) throw new Exception("No authorization.");
            var me = ExecutionEngine.ExecutingScriptHash;
            if (to == me)
            {
                Assert(CheckIsRouter(ExecutionEngine.CallingScriptHash), "Only support transfer to me by Router");
            }
            if (AssetStorage.Get(from) < amount) throw new Exception("Insufficient balance.");
            if (from == to) return true;

            AssetStorage.Reduce(from, amount);
            AssetStorage.Increase(to, amount);

            OnTransfer(from, to, amount);

            // Validate payable
            if (ContractManagement.GetContract(to) != null)
                Contract.Call(to, "onNEP17Payment", CallFlags.All, new object[] { from, amount, data });
            return true;
        }


        /// <summary>
        /// 接受nep17 token必备方法
        /// </summary>
        /// <param name="from"></param>
        /// <param name="amount"></param>
        /// <param name="data"></param>
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            //if (AssetStorage.GetPaymentStatus())
            //{
            //    if (ExecutionEngine.CallingScriptHash == NEO.Hash)
            //    {
            //        Mint(amount * TokensPerNEO);
            //    }
            //    else if (ExecutionEngine.CallingScriptHash == GAS.Hash)
            //    {
            //        if (from != null) Mint(amount * TokensPerGAS);
            //    }
            //    else
            //    {
            //        throw new Exception("Wrong calling script hash");
            //    }
            //}
            //else
            //{
            //    throw new Exception("Payment is disable on this contract!");
            //}
        }

        public static class TotalSupplyStorage
        {
            public static readonly string mapName = "contract";

            public static readonly string key = "totalSupply";

            public static void Increase(BigInteger value) => Put(Get() + value);

            public static void Reduce(BigInteger value) => Put(Get() - value);

            public static void Put(BigInteger value) => Storage.CurrentContext.CreateMap(mapName).Put(key, value);

            public static BigInteger Get()
            {
                var value = Storage.CurrentContext.CreateMap(mapName).Get(key);
                return value is null ? 0 : (BigInteger)value;
            }
        }


        public static class AssetStorage
        {
            public static readonly string mapName = "asset";

            public static void Increase(UInt160 key, BigInteger value) => Put(key, Get(key) + value);

            public static void Enable() => Storage.CurrentContext.CreateMap(mapName).Put("enable", 1);

            public static void Disable() => Storage.CurrentContext.CreateMap(mapName).Put("enable", 0);

            public static void Reduce(UInt160 key, BigInteger value)
            {
                var oldValue = Get(key);
                if (oldValue == value)
                    Remove(key);
                else
                    Put(key, oldValue - value);
            }

            public static void Put(UInt160 key, BigInteger value) => Storage.CurrentContext.CreateMap(mapName).Put(key, value);

            public static BigInteger Get(UInt160 key)
            {
                var value = Storage.CurrentContext.CreateMap(mapName).Get(key);
                return value is null ? 0 : (BigInteger)value;
            }

            public static bool GetPaymentStatus() => Storage.CurrentContext.CreateMap(mapName).Get("enable").Equals(1);

            public static void Remove(UInt160 key) => Storage.CurrentContext.CreateMap(mapName).Delete(key);
        }
    }
}
