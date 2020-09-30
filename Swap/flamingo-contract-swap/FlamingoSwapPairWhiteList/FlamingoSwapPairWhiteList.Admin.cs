using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace FlamingoSwapPairWhiteList
{
    partial class FlamingoSwapPairWhiteList
    {
        #region Admin

        #warning 检查此处的 Admin 地址是否为最新地址
        static readonly byte[] superAdmin = "AZaCs7GwthGy9fku2nFXtbrdKBRmrUQoFP".ToScriptHash();
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

    }
}
