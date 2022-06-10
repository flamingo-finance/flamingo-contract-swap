using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Attributes;

namespace ProxyTemplate
{
    public partial class ProxyTemplateContract
    {
        #region Admin

        [InitialValue("0xa006ba8e018d4529d0bd6f2d784c5ab67a15559f", ContractParameterType.Hash160)]
        static readonly UInt160 Router = default;

        [InitialValue("0xef9003443351ee3179a3f3ad9f1bef8273c83ecc", ContractParameterType.Hash160)]
        static readonly UInt160 Pair01 = default;

        [InitialValue("0xd02b79be5918eeeb065c427ade7fa629d6a50f93", ContractParameterType.Hash160)]
        static readonly UInt160 Token0 = default;

        [InitialValue("0x0db9f60de6684be8a6a5528692a1bd6b1ddbe944", ContractParameterType.Hash160)]
        static readonly UInt160 Token1 = default;

        [InitialValue("NdDvLrbtqeCVQkaLstAwh3md8SYYwqWRaE", ContractParameterType.Hash160)]
        static readonly UInt160 superAdmin = default;

        const string AdminKey = nameof(superAdmin);

        const byte Prefix_Allowed_Token0 = 0x00;
        const byte Prefix_Allowed_Token1 = 0x01;
        const byte Prefix_Allowed_LPToken = 0x02;

        const byte Prefix_Deposit_Balance0 = 0x03;
        const byte Prefix_Deposit_Balance1 = 0x04;
        const byte Prefix_Balance_LPToken = 0x05;

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
