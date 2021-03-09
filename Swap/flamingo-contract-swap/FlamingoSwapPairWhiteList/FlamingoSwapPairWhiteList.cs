using System;
using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;

namespace FlamingoSwapPairWhiteList
{
    [DisplayName("Flamingo Swap-Pair's WhiteList")]
    [ManifestExtra("Author", "Flamingo Finance")]
    [ManifestExtra("Email", "developer@flamingo.finance")]
    [ManifestExtra("Description", "This is a Flamingo Contract")]
    partial class FlamingoSwapPairWhiteList : SmartContract
    {

        /// <summary>
        /// 白名单存储区前缀，只允许一字节
        /// todo:换成0xff?
        /// </summary>
        private static readonly byte[] WhiteListPrefix = { 0x77 };


        #region 通知

        /// <summary>
        /// params: routerHash
        /// </summary>
        [DisplayName("addRouter")]
        private static event AddRouterEvent onAddRouter;
        private delegate void AddRouterEvent(UInt160 router);

        /// <summary>
        /// params: routerHash
        /// </summary>
        [DisplayName("removeRouter")]
        private static event RemoveRouterEvent onRemoveRouter;
        private delegate void RemoveRouterEvent(UInt160 router);


        #endregion

        /// <summary>
        /// 增加router白名单，加过白名单的router才能完成burn
        /// </summary>
        /// <param name="router">Nep5 tokenA</param>
        /// <returns></returns>
        public static bool AddRouter(UInt160 router)
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            var key = WhiteListPrefix.Concat((byte[])router);
            StoragePut(key, 1);
            onAddRouter(router);
            return true;
        }


        /// <summary>
        /// 移除router白名单，加过白名单的router才能完成burn
        /// </summary>
        /// <param name="router">Nep5 tokenA</param>
        /// <returns></returns>
        public static bool RemoveRouter(UInt160 router)
        {
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            var key = WhiteListPrefix.Concat((byte[])router);
            StorageDelete(key);
            onRemoveRouter(router);
            return true;
        }


        /// <summary>
        /// 检查router白名单
        /// </summary>
        /// <param name="router">Nep17 tokenA</param>
        /// <returns></returns>
        public static bool CheckRouter(UInt160 router)
        {
            var key = WhiteListPrefix.Concat(router);
            var value = ((byte[])StorageGet(key)).ToBigInteger();
            return value > 0;
        }


        /// <summary>
        /// 查询router白名单
        /// </summary>
        /// <returns></returns>
        public static UInt160[] GetAllRouter()
        {
            var iterator = (Iterator<KeyValue>)StorageFind(WhiteListPrefix);
            var result = new UInt160[0];
            while (iterator.Next())
            {
                var keyValue = iterator.Value;
                if (keyValue.Value > 0)
                {
                    Append(result, keyValue.Key);
                }
            }
            return result;
        }

    }
}
