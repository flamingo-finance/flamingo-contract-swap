using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract;
using Neo.SmartContract.Framework.Attributes;

namespace FlamingoSwapRouter
{
    public partial class FlamingoSwapRouterContract
    {
        #region Admin
        [InitialValue("NdDvLrbtqeCVQkaLstAwh3md8SYYwqWRaE", ContractParameterType.Hash160)]
        static readonly UInt160 superAdmin = default;

        [InitialValue("0xca2d20610d7982ebe0bed124ee7e9b2d580a6efc", ContractParameterType.Hash160)]
        static readonly UInt160 Factory = default;

        const string AdminKey = nameof(superAdmin);


        // When this contract address is included in the transaction signature,
        // this method will be triggered as a VerificationTrigger to verify that the signature is correct.
        // For example, this method needs to be called when withdrawing token from the contract.
        public static bool Verify() => Runtime.CheckWitness(GetAdmin());

        /// <summary>
        /// 获取合约管理员
        /// </summary>
        /// <returns></returns>
        public static UInt160 GetAdmin()
        {
            var admin = StorageGet(AdminKey);
            return admin?.Length == 20 ? (UInt160)admin : superAdmin;
        }

        /// <summary>
        /// 设置合约管理员
        /// </summary>
        /// <param name="admin"></param>
        /// <returns></returns>
        public static bool SetAdmin(UInt160 admin)
        {
            Assert(admin.IsValid && !admin.IsZero, "Invalid Address");
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            StoragePut(AdminKey, (ByteString)admin);
            return true;
        }



        #endregion


        #region Upgrade

        /// <summary>
        /// 升级
        /// </summary>
        /// <param name="nefFile"></param>
        /// <param name="manifest"></param>
        /// <param name="data"></param>
        public static void Update(ByteString nefFile, string manifest)
        {
            Assert(Verify(), "No authorization.");
            ContractManagement.Update(nefFile, manifest, null);
        }

        #endregion
    }
}
