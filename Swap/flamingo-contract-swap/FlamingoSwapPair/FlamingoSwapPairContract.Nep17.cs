using System.Numerics;
using Neo;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace FlamingoSwapPair
{
    partial class FlamingoSwapPairContract
    {
        public static ulong Decimals() => 8;
        public static BigInteger TotalSupply() => TotalSupplyStorage.Get();

        public static BigInteger BalanceOf(UInt160 account)
        {
            Assert(account.IsValid, "Invalid Account");
            return AssetStorage.Get(account);
        }

        public static bool Transfer(UInt160 from, UInt160 to, BigInteger amount, object data)
        {
            Assert(from.IsValid && to.IsValid, "Invalid From or To Address");
            Assert(amount > 0, "The parameter amount MUST be greater than 0.");
            Assert(Runtime.CheckWitness(from), "No authorization.");
            var me = Runtime.ExecutingScriptHash;
            if (to == me)
            {
                Assert(CheckIsRouter(Runtime.CallingScriptHash), "Not Allowed To Transfer");
            }
            Assert(AssetStorage.Get(from) >= amount, "Insufficient balance.");
            if (from == to) return true;

            AssetStorage.Reduce(from, amount);
            AssetStorage.Increase(to, amount);

            onTransfer(from, to, amount);

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
            //UInt160 asset = Runtime.CallingScriptHash;
            //Assert(asset == Token0 || asset == Token1, "Invalid Asset");
        }

        public static class TotalSupplyStorage
        {
            public static readonly string mapName = "contract";

            public static readonly string key = "totalSupply";

            public static void Increase(BigInteger value) => Put(Get() + value);

            public static void Reduce(BigInteger value) => Put(Get() - value);

            public static void Put(BigInteger value) => new StorageMap(Storage.CurrentContext, mapName).Put(key, value);

            public static BigInteger Get()
            {
                var value = new StorageMap(Storage.CurrentContext, mapName).Get(key);
                return value is null ? 0 : (BigInteger)value;
            }
        }


        public static class AssetStorage
        {
            public static readonly string mapName = "asset";

            public static void Increase(UInt160 key, BigInteger value) => Put(key, Get(key) + value);

            public static void Enable() => new StorageMap(Storage.CurrentContext, mapName).Put("enable", 1);

            public static void Disable() => new StorageMap(Storage.CurrentContext, mapName).Put("enable", 0);

            public static void Reduce(UInt160 key, BigInteger value)
            {
                var oldValue = Get(key);
                if (oldValue == value)
                    Remove(key);
                else
                    Put(key, oldValue - value);
            }

            public static void Put(UInt160 key, BigInteger value) => new StorageMap(Storage.CurrentContext, mapName).Put(key, value);

            public static BigInteger Get(UInt160 key)
            {
                var value = new StorageMap(Storage.CurrentContext, mapName).Get(key);
                return value is null ? 0 : (BigInteger)value;
            }

            public static bool GetPaymentStatus() => new StorageMap(Storage.CurrentContext, mapName).Get("enable").Equals(1);

            public static void Remove(UInt160 key) => new StorageMap(Storage.CurrentContext, mapName).Delete(key);
        }
    }
}
