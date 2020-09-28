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

        /// <summary>
        /// WhiteList 合约地址
        /// </summary>
        static readonly byte[] WhiteListContract = "3008f596f4fbdcaf712d6fc0ad2e9a522cc061cf".HexToBytes();


        const string AdminKey = nameof(superAdmin);
        private const string WhiteListContractKey = nameof(WhiteListContract);


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


        /// <summary>
        /// 获取WhiteListContract地址
        /// </summary>
        /// <returns></returns>
        public static byte[] GetWhiteListContract()
        {
            var whiteList = Storage.Get(WhiteListContractKey);
            return whiteList.Length == 20 ? whiteList : WhiteListContract;
        }

        /// <summary>
        /// 设置WhiteListContract地址
        /// </summary>
        /// <param name="whiteList"></param>
        /// <returns></returns>
        public static bool SetWhiteListContract(byte[] whiteList)
        {
            Assert(whiteList.Length == 20, "WhiteList contract Invalid");
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            Storage.Put(WhiteListContractKey, whiteList);
            return true;
        }

        /// <summary>
        /// 检查<see cref="callScript"/>是否为router合约
        /// </summary>
        /// <param name="callScript"></param>
        /// <returns></returns>
        private static bool CheckIsRouter(byte[] callScript)
        {
            var whiteList = GetWhiteListContract();
            return ((Func<string, object[], bool>)whiteList.ToDelegate())("checkRouter", new object[] { callScript });
        }

        #endregion


        #region Upgrade

        public static byte[] Upgrade(byte[] newScript, byte[] paramList, byte returnType, ContractPropertyState cps, string name, string version, string author, string email, string description)
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "upgrade: CheckWitness failed!");

            var me = ExecutionEngine.ExecutingScriptHash;
            byte[] newContractHash = Hash160(newScript);
            Assert(Blockchain.GetContract(newContractHash).Serialize().Equals(new byte[] { 0x00, 0x00 }), "upgrade: The contract already exists");

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
