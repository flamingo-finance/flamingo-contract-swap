using System;
using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;

namespace FlamingoSwapPairWhiteList
{
    [DisplayName("Flamingo Swap-Pair's Router WhiteList")]
    [ManifestExtra("Author", "Flamingo Finance")]
    [ManifestExtra("Email", "developer@flamingo.finance")]
    [ManifestExtra("Description", "This is a Flamingo Contract")]
    partial class FlamingoSwapPairWhiteList : SmartContract
    {

        /// <summary>
        /// 允许向LP合约转入LP token的白名单地址
        /// </summary>
        private const string WhiteList = "WhiteList";


        #region 通知

        /// <summary>
        /// params: routerHash
        /// </summary>
        [DisplayName("addRouter")]
        public static event Action<UInt160> onAddRouter;

        /// <summary>
        /// params: routerHash
        /// </summary>
        [DisplayName("removeRouter")]
        public static event Action<UInt160> onRemoveRouter;


        #endregion

        //public static object Main(string method, object[] args)
        //{
        //    if (Runtime.Trigger == TriggerType.Verification)
        //    {
        //        return Runtime.CheckWitness(GetAdmin());
        //    }
        //    else if (Runtime.Trigger == TriggerType.Application)
        //    {
        //        //合约调用时，等价以太坊的msg.sender
        //        //直接调用时，此处为 tx.Script.ToScriptHash();
        //        //var msgSender = ExecutionEngine.CallingScriptHash;
        //        if (method == "checkRouter") return CheckRouterWhiteList((byte[])args[0]);
        //        if (method == "addRouter") return AddRouterWhiteList((byte[])args[0]);
        //        if (method == "removeRouter") return RemoveRouterWhiteList((byte[])args[0]);
        //        if (method == "getAllRouter") return GetAllRouterWhiteList();
        //        if (method == "getAdmin") return GetAdmin();
        //        if (method == "setAdmin") return SetAdmin((byte[])args[0]);
        //    }
        //    return false;
        //}



        /// <summary>
        /// 增加router白名单，加过白名单的router才能完成burn
        /// </summary>
        /// <param name="router">Nep5 tokenA</param>
        /// <returns></returns>
        public static bool AddRouterWhiteList(UInt160 router)
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            var key = WhiteList.ToByteArray().Concat(router);
            StoragePut(key, 1);
            onAddRouter(router);
            return true;
        }


        /// <summary>
        /// 移除router白名单，加过白名单的router才能完成burn
        /// </summary>
        /// <param name="router">Nep5 tokenA</param>
        /// <returns></returns>
        public static bool RemoveRouterWhiteList(UInt160 router)
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            var key = WhiteList.ToByteArray().Concat(router);
            StorageDelete(key);
            onRemoveRouter(router);
            return true;
        }


        /// <summary>
        /// 检查router白名单
        /// </summary>
        /// <param name="router">Nep5 tokenA</param>
        /// <returns></returns>
        public static bool CheckRouterWhiteList(byte[] router)
        {
            if (router.Length != 20)
            {
                return false;
            }
            var key = WhiteList.ToByteArray().Concat(router);
            var value = ((byte[])StorageGet(key)).ToBigInteger();
            return value > 0;
        }


        ///// <summary>
        ///// 查询router白名单
        ///// todo:iterator key删了，暂时去掉此方法
        ///// </summary>
        ///// <returns></returns>
        //public static byte[][] GetAllRouterWhiteList()
        //{
        //    var iterator = StorageFind(WhiteList);
        //    var result = new byte[0][];
        //    while (iterator.Next())
        //    {
        //        if (((byte[])iterator.Value).ToBigInteger() > 0)
        //        {

        //            var router = iterator.Key.AsByteArray().Last(20);
        //            Append(result, router);
        //        }
        //    }
        //    return result;
        //}

    }
}
