﻿using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Attributes;

namespace ProxyTemplate
{
    public partial class ProxyTemplate : SmartContract
    {
        #region Admin

#warning Update the router address if necessary
        [InitialValue("0xa006ba8e018d4529d0bd6f2d784c5ab67a15559f", ContractParameterType.Hash160)]
        private static readonly UInt160 Router = default;

#warning Update the pair address if necessary
        [InitialValue("0xef9003443351ee3179a3f3ad9f1bef8273c83ecc", ContractParameterType.Hash160)]
        private static readonly UInt160 Pair01 = default;

#warning Update the token address if necessary
        [InitialValue("0xd02b79be5918eeeb065c427ade7fa629d6a50f93", ContractParameterType.Hash160)]
        private static readonly UInt160 Token0 = default;

#warning Update the token address if necessary
        [InitialValue("0x0db9f60de6684be8a6a5528692a1bd6b1ddbe944", ContractParameterType.Hash160)]
        private static readonly UInt160 Token1 = default;

        private const byte Prefix_Allowed_Token0 = 0x00;
        private const byte Prefix_Allowed_Token1 = 0x01;
        private const byte Prefix_Allowed_LPToken = 0x02;

        private const byte Prefix_Deposit_Balance0 = 0x03;
        private const byte Prefix_Deposit_Balance1 = 0x04;
        private const byte Prefix_Balance_LPToken = 0x05;

        public static void Update(ByteString nefFile, string manifest, object data)
        {
            ContractManagement.Update(nefFile, manifest, data);
        }
        #endregion
    }
}
