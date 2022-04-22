using System;
using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;

namespace Proxy
{
    [DisplayName("Proxy")]
    [ManifestExtra("Author", "NEO")]
    [ManifestExtra("Email", "developer@neo.org")]
    [ManifestExtra("Description", "This is a Proxy")]
    [ContractPermission("*")]
    public sealed class Proxy : SmartContract
    {
        [InitialValue("Nh1quymBgCUwjnxhJXeUdGM2axGXEzdqKF", ContractParameterType.Hash160)]
        private static readonly UInt160 InitialOwner = default;

        [InitialValue("0x4bbda65a836b1d2f6d8572c5c28b29cd0935fecd", ContractParameterType.Hash160)]
        private static readonly UInt160 Router = default;

        [InitialValue("0x8de7af3adc7d41e4a46cfdcccc33e50c25635913", ContractParameterType.Hash160)]
        private static readonly UInt160 Pair01 = default;

        [InitialValue("0x18a2a8c032bf77b1a4f8bdeac665ed817530f592", ContractParameterType.Hash160)]
        private static readonly UInt160 Token0 = default;

        [InitialValue("0x14dbf9feabea7b81df6553ca2d7a0f72c1b43085", ContractParameterType.Hash160)]
        private static readonly UInt160 Token1 = default;

        private const byte Prefix_Allowed_Token0 = 0x00;
        private const byte Prefix_Allowed_Token1 = 0x01;

        [DisplayName("Add")]
        public static event AddEvent onAdd;
        public delegate void AddEvent(BigInteger amount0Desired, BigInteger amount1Desired, BigInteger amount0Min, BigInteger amount1Min);

        [DisplayName("Approve")]
        public static event ApproveEvent onApprove;
        public delegate void ApproveEvent(UInt160 token, UInt160 to, BigInteger amount);

        [DisplayName("Retrieve")]
        public static event RetrieveEvent onRetrieve;
        public delegate void RetrieveEvent(UInt160 token, UInt160 to);

        [DisplayName("Fault")]
        public static event FaultEvent onFault;
        public delegate void FaultEvent(string message, params object[] paras);

        public static BigInteger[] ProxyAddLiquidity(BigInteger amount0Desired, BigInteger amount1Desired, BigInteger amount0Min, BigInteger amount1Min)
        {
            Assert(Runtime.CheckWitness(InitialOwner), "Forbidden");

            UInt160 me = Runtime.ExecutingScriptHash;
            BigInteger balance0 = (BigInteger)Contract.Call(Token0, "balanceOf", CallFlags.All, new object[] { me });
            BigInteger balance1 = (BigInteger)Contract.Call(Token1, "balanceOf", CallFlags.All, new object[] { me });

            Assert(amount0Desired >= 0 && amount1Desired >= 0, "Insufficient parameters");
            Assert(Approve(Token0, Pair01, amount0Desired), "Insufficient Token0 balance");
            onApprove(Token0, Pair01, amount0Desired);
            Assert(Approve(Token1, Pair01, amount1Desired), "Insufficient Token1 balance");
            onApprove(Token1, Pair01, amount1Desired);

            var result = Contract.Call(Router, "addLiquidity", CallFlags.All, new object[] { me, Token0, Token1, amount0Desired, amount1Desired, amount0Min, amount1Min, 12345678901234567 });
            onAdd(amount0Desired, amount1Desired, amount0Min, amount1Min);

            Retrieve(Token0, Pair01);
            onRetrieve(Token0, Pair01);
            Retrieve(Token1, Pair01);
            onRetrieve(Token1, Pair01);

            return (BigInteger[])result;
        }

        public static void onNEP17Payment(UInt160 from, BigInteger amount, BigInteger data)
        {

        }

        public static bool ApprovedTransfer(UInt160 token, UInt160 to, BigInteger amount, byte[] data)
        {
            byte prefix = 0x00;
            if (token == Token0)
            {
                prefix = Prefix_Allowed_Token0;
            }
            else if (token == Token1)
            {
                prefix = Prefix_Allowed_Token1;
            }
            else
            {
                return false;
            }

            StorageMap allowedMap = new(Storage.CurrentContext, prefix);
            UInt160 me = Runtime.ExecutingScriptHash;
            BigInteger allowedAmount = (BigInteger)allowedMap.Get(to);

            if (amount <= allowedAmount)
            {
                return (bool)Contract.Call(token, "transfer", CallFlags.All, new object[] { me, to, amount, data });
            }
            else
            {
                return false;
            }
        }

        private static bool Approve(UInt160 token, UInt160 to, BigInteger amount)
        {
            byte prefix = 0x00;
            if (token == Token0)
            {
                prefix = Prefix_Allowed_Token0;
            }
            else if (token == Token1)
            {
                prefix = Prefix_Allowed_Token1;
            }
            else
            {
                return false;
            }

            StorageMap allowedMap = new(Storage.CurrentContext, prefix);
            UInt160 me = Runtime.ExecutingScriptHash;
            BigInteger balance = (BigInteger)Contract.Call(token, "balanceOf", CallFlags.All, new object[] { me });
            if (balance >= amount)
            {
                allowedMap.Put(to, amount);
                return true;
            }
            else
            {
                return false;
            }
        }

        private static bool Retrieve(UInt160 token, UInt160 to)
        {
            byte prefix = 0x00;
            if (token == Token0)
            {
                prefix = Prefix_Allowed_Token0;
            }
            else if (token == Token1)
            {
                prefix = Prefix_Allowed_Token1;
            }
            else
            {
                return false;
            }

            StorageMap allowedMap = new(Storage.CurrentContext, prefix);
            allowedMap.Delete(to);
            return true;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                onFault(message, null);
                ExecutionEngine.Assert(false);
            }
        }
        
        public static void Update(ByteString nefFile, string manifest, object data)
        {
            ContractManagement.Update(nefFile, manifest, data);
        }
    }
}
