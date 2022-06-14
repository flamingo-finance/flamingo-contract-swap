using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace FlamingoSwapOrderBook
{
    public partial class FlamingoSwapOrderBookContract
    {
        #region Admin

#warning Update the admin address if necessary
        [InitialValue("NdDvLrbtqeCVQkaLstAwh3md8SYYwqWRaE", ContractParameterType.Hash160)]
        static readonly UInt160 superAdmin = default;
        [InitialValue("0xd6abe115ecb75e1fa0b42f5e85934ce8c1ae2893", ContractParameterType.Hash160)]
        static readonly UInt160 bNEO = default;

        const string AdminKey = nameof(superAdmin);
        const string GASAdminKey = nameof(GASAdminKey);
        const string FundAddresskey = nameof(FundAddresskey);

        private static readonly byte[] OrderIDKey = new byte[] { 0x00 };
        private static readonly byte[] BookMapKey = new byte[] { 0x01 };
        private static readonly byte[] OrderMapKey = new byte[] { 0x02 };
        private static readonly byte[] ReceiptMapKey = new byte[] { 0x03 };

        // When this contract address is included in the transaction signature,
        // this method will be triggered as a VerificationTrigger to verify that the signature is correct.
        // For example, this method needs to be called when withdrawing token from the contract.
        [Safe]
        public static bool Verify() => Runtime.CheckWitness(GetAdmin());

        [Safe]
        public static UInt160 GetAdmin()
        {
            var admin = StorageGet(AdminKey);
            return admin?.Length == 20 ? (UInt160)admin : superAdmin;
        }

        public static bool SetAdmin(UInt160 admin)
        {
            Assert(Verify(), "No Authorization");
            Assert(admin.IsValid && !admin.IsZero, "Invalid Address");
            StoragePut(AdminKey, admin);
            return true;
        }

        public static void ClaimGASFrombNEO(UInt160 receiveAddress)
        {
            Assert(Runtime.CheckWitness(GetGASAdmin()), "Forbidden");
            var me = Runtime.ExecutingScriptHash;
            var beforeBalance = GAS.BalanceOf(me);
            Assert((bool)Contract.Call(bNEO, "transfer", CallFlags.All, Runtime.ExecutingScriptHash, bNEO, 0, null), "claim fail");
            var afterBalance = GAS.BalanceOf(me);

            GAS.Transfer(me, receiveAddress, afterBalance - beforeBalance);
        }

        [Safe]
        public static UInt160 GetGASAdmin()
        {
            var admin = StorageGet(GASAdminKey);
            return (UInt160)admin;
        }

        public static bool SetGASAdmin(UInt160 GASAdmin)
        {
            Assert(GASAdmin.IsAddress(), "Invalid Address");
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            StoragePut(GASAdminKey, GASAdmin);
            return true;
        }
        #endregion

        #region FundFee

        [Safe]
        public static UInt160 GetFundAddress()
        {
            var address = StorageGet(FundAddresskey);
            return address?.Length == 20 ? (UInt160)address : null;
        }

        public static bool SetFundAddress(UInt160 address)
        {
            Assert(address.IsAddress(), "Invalid Address");
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            StoragePut(FundAddresskey, address);
            return true;
        }
        #endregion

        #region Upgrade

        public static void Update(ByteString nefFile, string manifest, object data)
        {
            Assert(Verify(), "No Authorization");
            ContractManagement.Update(nefFile, manifest, data);
        }
        #endregion
    }
}
