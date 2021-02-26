using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;

namespace FlamingoSwapPair
{
    partial class FlamingoSwapPairContract
    {

        #region Admin

        static readonly UInt160 superAdmin = "AVB7PZUpfZShoP8ih4krcCV5Z1SdpxQX3B".ToScriptHash();

        /// <summary>
        /// WhiteList 合约地址
        /// </summary>
        static readonly UInt160 WhiteListContract = (UInt160)"3008f596f4fbdcaf712d6fc0ad2e9a522cc061cf".HexToBytes();

        const string AdminKey = nameof(superAdmin);
        private const string WhiteListContractKey = nameof(WhiteListContract);

        // When this contract address is included in the transaction signature,
        // this method will be triggered as a VerificationTrigger to verify that the signature is correct.
        // For example, this method needs to be called when withdrawing token from the contract.
        public static bool Verify() => Runtime.CheckWitness(GetAdmin());

        #endregion

        #region TokenAB

        ///// <summary>
        ///// Token 0 地址(Token0放置合约hash小的token)
        ///// </summary>
        //static readonly byte[] Token0 = "7c76490fc79a8a47068b904e83d78c0292590fd4".HexToBytes();

        ///// <summary>
        /////  Token 1 地址
        ///// </summary>
        //static readonly byte[] Token1 = "cbad1e6082cb71f336939934f21e5929a5c6d7ff".HexToBytes();


        //[DisplayName("symbol")]
        //public static string Symbol() => "E-AB"; //symbol of the token

        #endregion

        #region TokenBC

        ///// <summary>
        ///// Token 0 地址(Token0放置合约hash小的token)
        ///// </summary>
        //static readonly byte[] Token0 = "f84be0412caec8e34a38eadf430734b1b65deab9".HexToBytes();

        ///// <summary>
        /////  Token 1 地址
        ///// </summary>
        //static readonly byte[] Token1 = "7c76490fc79a8a47068b904e83d78c0292590fd4".HexToBytes();

        //[DisplayName("symbol")]
        //public static string Symbol() => "E-BC"; //symbol of the token

        #endregion

        #region pnWETH-nNEO

        ///// <summary>
        ///// nNEO 0 地址(Token0放置合约hash小的token)
        ///// 0x17da3881ab2d050fea414c80b3fa8324d756f60e
        ///// </summary>
        //static readonly byte[] Token0 = "0ef656d72483fab3804c41ea0f052dab8138da17".HexToBytes();

        ///// <summary>
        /////  pnWETH 1 地址
        /////  0x23535b6fd46b8f867ed010bab4c2bd8ef0d0c64f
        ///// </summary>
        //static readonly byte[] Token1 = "4fc6d0f08ebdc2b4ba10d07e868f6bd46f5b5323".HexToBytes();


        //[DisplayName("symbol")]
        //public static string Symbol() => "pnWETH-nNEO"; //symbol of the token

        #endregion

        #region pnWBTC-nNEO

        ///// <summary>
        ///// nNEO 0 地址(Token0放置合约hash小的token)
        ///// 0x17da3881ab2d050fea414c80b3fa8324d756f60e
        ///// </summary>
        //static readonly byte[] Token0 = "0ef656d72483fab3804c41ea0f052dab8138da17".HexToBytes();

        ///// <summary>
        /////  pnWBTC 1 地址
        /////  0x69c57a716567a0f6910a0b3c1d4508fa163eb927
        ///// </summary>
        //static readonly byte[] Token1 = "27b93e16fa08451d3c0b0a91f6a06765717ac569".HexToBytes();


        //[DisplayName("symbol")]
        //public static string Symbol() => "pnWBTC-nNEO"; //symbol of the token

        #endregion

        #region pONT-nNEO

        /// <summary>
        /// nNEO 0 地址(Token0放置合约hash小的token)
        /// 0x17da3881ab2d050fea414c80b3fa8324d756f60e
        /// </summary>
        static readonly byte[] Token0 = "0ef656d72483fab3804c41ea0f052dab8138da17".HexToBytes();

        /// <summary>
        ///  pONT 1 地址
        ///  0x658cabf9c1f71ba0fa64098a7c17e52b94046ece
        /// </summary>
        static readonly byte[] Token1 = "ce6e04942be5177c8a0964faa01bf7c1f9ab8c65".HexToBytes();


        [DisplayName("symbol")]
        public static string Symbol() => "pONT-nNEO"; //symbol of the token

        #endregion

        #region FLM-nNEO

        ///// <summary>
        ///// FLM 地址(Token0放置合约hash小的token)
        ///// 0x083ea8071188c7fe5b5e4af96ded222670d76663
        ///// </summary>
        //static readonly byte[] Token0 = "6366d7702622ed6df94a5e5bfec7881107a83e08".HexToBytes();

        ///// <summary>
        /////  nNEO 地址
        /////  0x17da3881ab2d050fea414c80b3fa8324d756f60e
        ///// </summary>
        //static readonly byte[] Token1 = "0ef656d72483fab3804c41ea0f052dab8138da17".HexToBytes();


        //[DisplayName("symbol")]
        //public static string Symbol() => "FLM-nNEO"; //symbol of the token

        #endregion


        /// <summary>
        /// 获取合约管理员
        /// </summary>
        /// <returns></returns>
        public static UInt160 GetAdmin()
        {
            var admin = StorageGet(AdminKey);
            return admin.Length == 20 ? (UInt160)admin : superAdmin;
        }

        /// <summary>
        /// 设置合约管理员
        /// </summary>
        /// <param name="admin"></param>
        /// <returns></returns>
        public static bool SetAdmin(UInt160 admin)
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            StoragePut(AdminKey, admin);
            return true;
        }


        /// <summary>
        /// 获取WhiteListContract地址
        /// </summary>
        /// <returns></returns>
        public static UInt160 GetWhiteListContract()
        {
            var whiteList = StorageGet(WhiteListContractKey);
            return whiteList.Length == 20 ? (UInt160)whiteList : WhiteListContract;
        }

        /// <summary>
        /// 设置WhiteListContract地址
        /// </summary>
        /// <param name="whiteList"></param>
        /// <returns></returns>
        public static bool SetWhiteListContract(UInt160 whiteList)
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            StoragePut(WhiteListContractKey, whiteList);
            return true;
        }

        /// <summary>
        /// 检查<see cref="callScript"/>是否为router合约
        /// </summary>
        /// <param name="callScript"></param>
        /// <returns></returns>
        private static bool CheckIsRouter(UInt160 callScript)
        {
            var whiteList = GetWhiteListContract();
            return ((Func<string, object[], bool>)((byte[])whiteList).ToDelegate())("checkRouter", new object[] { callScript });
        }


        #region Upgrade

        //todo:升级
        //public static byte[] Upgrade(byte[] newScript, byte[] paramList, byte returnType, ContractPropertyState cps, string name, string version, string author, string email, string description)
        //{
        //    Assert(Runtime.CheckWitness(GetAdmin()), "upgrade: CheckWitness failed!");

        //    var me = ExecutionEngine.ExecutingScriptHash;
        //    byte[] newContractHash = Hash160(newScript);
        //    Assert(Blockchain.GetContract(newContractHash).Serialize().Equals(new byte[] { 0x00, 0x00 }), "upgrade: The contract already exists");

        //    var r = ReservePair;
        //    SafeTransfer(Token0, me, newContractHash, r.Reserve0);
        //    SafeTransfer(Token1, me, newContractHash, r.Reserve1);

        //    Contract newContract = Contract.Migrate(newScript, paramList, returnType, cps, name, version, author, email, description);

        //    Runtime.Notify("upgrade", ExecutionEngine.ExecutingScriptHash, newContractHash);
        //    return newContractHash;
        //}


        #endregion
    }
}
