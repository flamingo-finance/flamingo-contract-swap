using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;

namespace FlamingoSwapPair
{
    partial class FlamingoSwapPairContract
    {

        #region Admin

        static readonly byte[] superAdmin = "AZaCs7GwthGy9fku2nFXtbrdKBRmrUQoFP".ToScriptHash();

        ///// <summary>
        ///// Factory地址
        ///// </summary>
        //static readonly byte[] FactoryContract = "32904da4441d544d05074774ade7c891d912f61a".HexToBytes();

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

            var me = ExecutionEngine.ExecutingScriptHash;
            byte[] newContractHash = Hash160(newScript);

            var r = ReservePair;
            SafeTransfer(Token0, me, newContractHash, r.Reserve0);
            SafeTransfer(Token1, me, newContractHash, r.Reserve1);

            Contract newContract = Contract.Migrate(newScript, paramList, returnType, cps, name, version, author, email, description);

            Runtime.Notify("upgrade", ExecutionEngine.ExecutingScriptHash, newContractHash);
            return newContractHash;
        }


        #endregion
    }
}
