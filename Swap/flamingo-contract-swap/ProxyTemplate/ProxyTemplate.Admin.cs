using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Attributes;

namespace ProxyTemplate
{
    public partial class ProxyTemplate : SmartContract
    {
        #region Admin
        [InitialValue("0x4bbda65a836b1d2f6d8572c5c28b29cd0935fecd", ContractParameterType.Hash160)]
        private static readonly UInt160 Router = default;

        [InitialValue("0x8f83e734cc97e645efdf1e12d32539a5191460ab", ContractParameterType.Hash160)]
        private static readonly UInt160 Pair01 = default;

        [InitialValue("0x18a2a8c032bf77b1a4f8bdeac665ed817530f592", ContractParameterType.Hash160)]
        private static readonly UInt160 Token0 = default;

        [InitialValue("0x14dbf9feabea7b81df6553ca2d7a0f72c1b43085", ContractParameterType.Hash160)]
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
