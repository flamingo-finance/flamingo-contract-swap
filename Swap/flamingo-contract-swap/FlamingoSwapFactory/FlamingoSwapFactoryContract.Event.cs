﻿using System.ComponentModel;
using Neo;

namespace FlamingoSwapFactory
{
    partial class FlamingoSwapFactoryContract
    {
        /// <summary>
        /// params: tokenA,tokenB,exchangeContractHash
        /// </summary>
        [DisplayName("createExchange")]
        public static event CreateExchangeEvent onCreateExchange;
        public delegate void CreateExchangeEvent(UInt160 tokenA, UInt160 tokenB, UInt160 exchangeContractHash);

        /// <summary>
        /// params: tokenA,tokenB
        /// </summary>
        [DisplayName("removeExchange")]
        public static event RemoveExchangeEvent onRemoveExchange;
        public delegate void RemoveExchangeEvent(UInt160 tokenA, UInt160 tokenB);


        /// <summary>
        /// params: message, extend data
        /// </summary>
        [DisplayName("fault")]
        public static event FaultEvent onFault;
        public delegate void FaultEvent(string message, params object[] paras);

    }
}
