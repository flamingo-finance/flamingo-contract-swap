using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace FlamingoSwapOrderBook
{
    public partial class FlamingoSwapOrderBookContract : SmartContract
    {
        #region Admin

#warning Update the admin address if necessary
        [InitialValue("NdDvLrbtqeCVQkaLstAwh3md8SYYwqWRaE", ContractParameterType.Hash160)]
        static readonly UInt160 superAdmin = default;
        const string AdminKey = nameof(superAdmin);

        private static readonly byte[] BookMapKey = new byte[] { 0x00 };
        private static readonly byte[] OrderMapKey = new byte[] { 0x01 };

        // When this contract address is included in the transaction signature,
        // this method will be triggered as a VerificationTrigger to verify that the signature is correct.
        // For example, this method needs to be called when withdrawing token from the contract.
        public static bool Verify() => Runtime.CheckWitness(GetAdmin());

        public static UInt160 GetAdmin()
        {
            var admin = Storage.Get(Storage.CurrentContext, AdminKey);
            return admin?.Length == 20 ? (UInt160)admin : superAdmin;
        }

        public static bool SetAdmin(UInt160 admin)
        {
            Assert(Verify(), "No Authorization");
            Assert(admin.IsValid && !admin.IsZero, "Invalid Address");
            Storage.Put(Storage.CurrentContext, AdminKey, admin);
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