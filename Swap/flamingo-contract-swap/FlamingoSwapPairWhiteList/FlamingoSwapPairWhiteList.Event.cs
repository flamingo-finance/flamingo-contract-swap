﻿using System.ComponentModel;
using Neo;

namespace FlamingoSwapPairWhiteList
{
    partial class FlamingoSwapPairWhiteList
    {

        /// <summary>
        /// params: routerHash
        /// </summary>
        [DisplayName("AddRouter")]
        public static event AddRouterEvent onAddRouter;
        public delegate void AddRouterEvent(UInt160 router);

        /// <summary>
        /// params: routerHash
        /// </summary>
        [DisplayName("RemoveRouter")]
        public static event RemoveRouterEvent onRemoveRouter;
        public delegate void RemoveRouterEvent(UInt160 router);

    }
}
