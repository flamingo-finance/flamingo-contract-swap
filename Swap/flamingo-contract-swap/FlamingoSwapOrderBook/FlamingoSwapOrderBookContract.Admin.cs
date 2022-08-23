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
        [InitialValue("NdrUjmLFCmr6RjM52njho5sFUeeTdKPxG9", ContractParameterType.Hash160)]
        static readonly UInt160 superAdmin = default;

        [InitialValue("0xc0695bdb8a87a40aff33c73ff6349ccc05fa9f01", ContractParameterType.Hash160)]
        static readonly UInt160 Factory = default;

        [InitialValue("0xd6abe115ecb75e1fa0b42f5e85934ce8c1ae2893", ContractParameterType.Hash160)]
        static readonly UInt160 bNEO = default;

        static readonly uint ORDER_PER_PAGE = 1 << 8;

        private const string AdminKey = nameof(superAdmin);
        private const string GASAdminKey = nameof(GASAdminKey);
        private const string FundAddresskey = nameof(FundAddresskey);

        private static readonly byte[] OrderCounterKey = new byte[] { 0x00 };
        private static readonly byte[] PageCounterKey = new byte[] { 0x01 };
        private static readonly byte[] BookMapPrefix = new byte[] { 0x02 };
        private static readonly byte[] PageMapPrefix = new byte[] { 0x03 };
        private static readonly byte[] OrderIndexKey = new byte[] { 0x04 };
        private static readonly byte[] OrderMapPrefix = new byte[] { 0x05 };

        // When this contract address is included in the transaction signature,
        // this method will be triggered as a VerificationTrigger to verify that the signature is correct.
        // For example, this method needs to be called when withdrawing token from the contract.
        [Safe]
        public static bool Verify() => Runtime.CheckWitness(GetAdmin());

        [Safe]
        public static UInt160 GetAdmin()
        {
            var admin = Storage.Get(Storage.CurrentReadOnlyContext, AdminKey);
            return admin?.Length == 20 ? (UInt160)admin : superAdmin;
        }

        public static bool SetAdmin(UInt160 admin)
        {
            Assert(Verify(), "No Authorization");
            Assert(admin.IsAddress(), "Invalid Address");
            Storage.Put(Storage.CurrentContext, AdminKey, admin);
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
            var address = Storage.Get(Storage.CurrentReadOnlyContext, GASAdminKey);
            return address?.Length == 20 ? (UInt160)address : null;
        }

        public static bool SetGASAdmin(UInt160 GASAdmin)
        {
            Assert(GASAdmin.IsAddress(), "Invalid Address");
            Assert(Verify(), "No Authorization");
            Storage.Put(Storage.CurrentContext, GASAdminKey, GASAdmin);
            return true;
        }
        #endregion

        #region FundFee

        [Safe]
        public static UInt160 GetFundAddress()
        {
            var address = Storage.Get(Storage.CurrentReadOnlyContext, FundAddresskey);
            return address?.Length == 20 ? (UInt160)address : null;
        }

        public static bool SetFundAddress(UInt160 address)
        {
            Assert(address.IsAddress(), "Invalid Address");
            Assert(Verify(), "No Authorization");
            Storage.Put(Storage.CurrentContext, FundAddresskey, address);
            return true;
        }
        #endregion

        #region Upgrade

        public static void Update(ByteString nefFile, string manifest)
        {
            Assert(Verify(), "No Authorization");
            ContractManagement.Update(nefFile, manifest, null);
        }
        #endregion
    }
}
