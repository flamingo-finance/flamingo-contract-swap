using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services;

namespace ProxyTemplate
{
    public partial class ProxyTemplate : SmartContract
    {
        public static bool ApprovedTransfer(UInt160 token, UInt160 to, BigInteger amount, byte[] data)
        {
            // Check token
            Assert(token == Token0 || token == Token1 || token == Pair01, "Unsupported Token");

            // Find allowed
            BigInteger allowedAmount = AllowedOf(token, to);

            if (amount <= allowedAmount)
            {
                // Transfer
                Consume(token, to, amount);
                UInt160 me = Runtime.ExecutingScriptHash;
                return (bool)Contract.Call(token, "transfer", CallFlags.All, new object[] { me, to, amount, data });
            }
            else
            {
                return false;
            }
        }

        public static void onNEP17Payment(UInt160 from, BigInteger amount, BigInteger data)
        {

        }

        private static bool Approve(UInt160 token, UInt160 from, UInt160 to, BigInteger amount)
        {
            BigInteger balance = DepositOf(token, from);
            if (balance >= amount)
            {
                UpdateBalance(token == Token0 ? Prefix_Allowed_Token0 : (token == Token1 ? Prefix_Allowed_Token1 : Prefix_Allowed_LPToken), to, +amount);
                return true;
            }
            else
            {
                return false;
            }
        }

        private static bool Consume(UInt160 token, UInt160 to, BigInteger amount)
        {
            BigInteger allowed = AllowedOf(token, to);
            if (allowed >= amount)
            {
                UpdateBalance(token == Token0 ? Prefix_Allowed_Token0 : (token == Token1 ? Prefix_Allowed_Token1 : Prefix_Allowed_LPToken), to, -amount);
                return true;
            }
            else
            {
                return false;
            }
        }

        private static bool Retrieve(UInt160 token, UInt160 to)
        {
            BigInteger allowed = AllowedOf(token, to);
            UpdateBalance(token == Token0 ? Prefix_Allowed_Token0 : (token == Token1 ? Prefix_Allowed_Token1 : Prefix_Allowed_LPToken), to, -allowed);
            return true;
        }

        private static BigInteger AllowedOf(UInt160 token, UInt160 to)
        {
            StorageMap allowedMap = new(Storage.CurrentContext, token == Token0 ? Prefix_Allowed_Token0 : (token == Token1 ? Prefix_Allowed_Token1 : Prefix_Allowed_LPToken));
            return (BigInteger)allowedMap.Get(to);
        }

        private static bool YMint(UInt160 token, UInt160 owner, BigInteger amount)
        {
            return UpdateBalance(token == Token0 ? Prefix_Deposit_Balance0 : (token == Token1 ? Prefix_Deposit_Balance1 : Prefix_Balance_LPToken), owner, +amount);
        }

        private static bool YBurn(UInt160 token, UInt160 owner, BigInteger amount)
        {
            return UpdateBalance(token == Token0 ? Prefix_Deposit_Balance0 : (token == Token1 ? Prefix_Deposit_Balance1 : Prefix_Balance_LPToken), owner, -amount);
        }

        public static BigInteger DepositOf(UInt160 token, UInt160 owner)
        {
            StorageMap depositMap = new(Storage.CurrentContext, token == Token0 ? Prefix_Deposit_Balance0 : (token == Token1 ? Prefix_Deposit_Balance1 : Prefix_Balance_LPToken));
            return (BigInteger)depositMap.Get(owner);
        }

        private static bool UpdateBalance(byte prefix, UInt160 owner, BigInteger increment)
        {
            StorageMap balanceMap = new(Storage.CurrentContext, prefix);
            BigInteger balance = (BigInteger)balanceMap[owner];
            balance += increment;
            if (balance < 0) return false;
            if (balance.IsZero)
                balanceMap.Delete(owner);
            else
                balanceMap.Put(owner, balance);
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

        private static void Assert(bool condition, string message, object data = null)
        {
            if (!condition)
            {
                onFault(message, data);
                ExecutionEngine.Assert(false);
            }
        }
    }
}
