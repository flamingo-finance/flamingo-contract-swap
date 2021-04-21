using System;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract;

namespace FlamingoSwapRouter
{
    public partial class FlamingoSwapRouterContract
    {


        #region Admin

#warning 检查此处的 Admin 地址是否为最新地址
        [InitialValue("NMA2FKN8up2cEwaJgtmAiDrZWB69ApnDfp", ContractParameterType.Hash160)]
        static readonly UInt160 superAdmin = default;

#warning 检查此处的 Factory 地址是否为最新地址
        //注意此处输入小端序
        [InitialValue("d6103a999109e6b9d4592a0d34a2ac3c034f197e", ContractParameterType.ByteArray)]
        static readonly byte[] Factory = default;

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
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            StoragePut(AdminKey, (ByteString)admin);
            return true;
        }



        #endregion


        #region Upgrade

        //todo:升级
        //public static byte[] Upgrade(byte[] newScript, byte[] paramList, byte returnType, ContractPropertyState cps, string name, string version, string author, string email, string description)
        //{
        //    Assert(Runtime.CheckWitness(GetAdmin()), "upgrade: CheckWitness failed!");

        //    byte[] newContractHash = Hash160(newScript);
        //    Assert(Blockchain.GetContract(newContractHash).Serialize().Equals(new byte[] { 0x00, 0x00 }), "upgrade: The contract already exists");

        //    Contract newContract = Contract.Migrate(newScript, paramList, returnType, cps, name, version, author, email, description);
        //    Runtime.Notify("upgrade", ExecutionEngine.ExecutingScriptHash, newContractHash);
        //    return newContractHash;
        //}



        /// <summary>
        /// 升级
        /// </summary>
        /// <param name="nefFile"></param>
        /// <param name="manifest"></param>
        public static void Update(ByteString nefFile, string manifest)
        {
            if (!Verify()) throw new Exception("No authorization.");
            ContractManagement.Update(nefFile, manifest, null);
        }

        #endregion
    }
}
