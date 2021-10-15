using System;
using System.Numerics;
using System.ComponentModel;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Native;

namespace FlamingoSwapPair
{
    partial class FlamingoSwapPairContract
    {
        #region Settings

#warning 检查此处的 Admin 地址是否为最新地址
        [InitialValue("NPS3U9PduobRCai5ZUdK2P3Y8RjwzMVfSg", Neo.SmartContract.ContractParameterType.Hash160)]
        static readonly UInt160 superAdmin = default;

#warning 检查此处的 WhiteList 地址是否为最新地址
        /// <summary>
        /// WhiteList 合约地址
        /// </summary>
        [InitialValue("0x2b71423ef064a0ae424f76e8ce67336301334e38", Neo.SmartContract.ContractParameterType.Hash160)]
        static readonly UInt160 WhiteListContract = default;

        #region TokenAB


        [DisplayName("symbol")]
        public static string Symbol() => "E-AB"; //symbol of the token

        /// <summary>
        /// 两个token地址，无需排序
        /// </summary>
        [InitialValue("0xd56799d3d1dbcea66398a68f6761188972db3d42", Neo.SmartContract.ContractParameterType.Hash160)]
        static readonly UInt160 TokenA = default;
        [InitialValue("0xe35b29ea335d96a26e6150f8866c87fa7fd187d7", Neo.SmartContract.ContractParameterType.Hash160)]
        static readonly UInt160 TokenB = default;


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

        ///// <summary>
        ///// nNEO 0 地址(Token0放置合约hash小的token)
        ///// 0x17da3881ab2d050fea414c80b3fa8324d756f60e
        ///// </summary>
        //static readonly byte[] Token0 = "0ef656d72483fab3804c41ea0f052dab8138da17".HexToBytes();

        ///// <summary>
        /////  pONT 1 地址
        /////  0x658cabf9c1f71ba0fa64098a7c17e52b94046ece
        ///// </summary>
        //static readonly byte[] Token1 = "ce6e04942be5177c8a0964faa01bf7c1f9ab8c65".HexToBytes();


        //[DisplayName("symbol")]
        //public static string Symbol() => "pONT-nNEO"; //symbol of the token

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

        #endregion

        #region Admin


        const string AdminKey = nameof(superAdmin);
        private const string WhiteListContractKey = nameof(WhiteListContract);

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
            StoragePut(AdminKey, admin);
            return true;
        }

        public static void ClaimRewardFrombNEO(UInt160 bNEOAddress) 
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            Assert((bool)Contract.Call(bNEOAddress, "transfer", CallFlags.All, Runtime.ExecutingScriptHash, bNEOAddress, 0), "claim fail");
        }

        public static void ReceiveGas(UInt160 address, BigInteger amount) 
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            GAS.Transfer(Runtime.ExecutingScriptHash, address, amount);
        }

        #endregion

        #region WhiteContract


        /// <summary>
        /// 获取WhiteListContract地址
        /// </summary>
        /// <returns></returns>
        public static UInt160 GetWhiteListContract()
        {
            var whiteList = StorageGet(WhiteListContractKey);
            return whiteList?.Length == 20 ? (UInt160)whiteList : WhiteListContract;
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
        public static bool CheckIsRouter(UInt160 callScript)
        {
            var whiteList = GetWhiteListContract();
            return (bool)Contract.Call(whiteList, "checkRouter", CallFlags.All, new object[] { callScript });
        }

        #endregion

        #region Upgrade


        /// <summary>
        /// 升级
        /// </summary>
        /// <param name="nefFile"></param>
        /// <param name="manifest"></param>
        /// <param name="data"></param>
        public static void Update(ByteString nefFile, string manifest, object data)
        {
            if (!Verify()) throw new Exception("No authorization.");
            ContractManagement.Update(nefFile, manifest, data);
        }

        #endregion
    }
}
