using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;

namespace FlamingoSwapRouter
{
    partial class FlamingoSwapRouterContract
    {
   

        #region Admin

        #warning 检查此处的 Admin 地址是否为最新地址
        static readonly byte[] superAdmin = "AZaCs7GwthGy9fku2nFXtbrdKBRmrUQoFP".ToScriptHash();

        #warning 检查此处的 Factory 地址是否为最新地址
        static readonly byte[] Factory = "1b099f38376e27dbffcac05ee0e670d81a3c61f8".HexToBytes();

        const string AdminKey = nameof(superAdmin);


        /// <summary>
        /// 获取合约管理员
        /// </summary>
        /// <returns></returns>
        public static byte[] GetAdmin()
        {
            var admin = Storage.Get(AdminKey);
            return admin.Length == 20 ? admin : superAdmin;
        }

        /// <summary>
        /// 设置合约管理员
        /// </summary>
        /// <param name="admin"></param>
        /// <returns></returns>
        public static bool SetAdmin(byte[] admin)
        {
            Assert(admin.Length == 20, "NewAdmin Invalid");
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            Storage.Put(AdminKey, admin);
            return true;
        }



        #endregion


        #region Upgrade

        public static byte[] Upgrade(byte[] newScript, byte[] paramList, byte returnType, ContractPropertyState cps, string name, string version, string author, string email, string description)
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "upgrade: CheckWitness failed!");

            byte[] newContractHash = Hash160(newScript);
            Assert(Blockchain.GetContract(newContractHash).Serialize().Equals(new byte[] { 0x00, 0x00 }), "upgrade: The contract already exists");

            Contract newContract = Contract.Migrate(newScript, paramList, returnType, cps, name, version, author, email, description);
            Runtime.Notify("upgrade", ExecutionEngine.ExecutingScriptHash, newContractHash);
            return newContractHash;
        }


        #endregion
    }
}
