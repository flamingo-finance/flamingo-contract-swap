using System;
using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services;

namespace FlashLoanTemple
{
    [DisplayName("FlashLoan")]
    [ManifestExtra("Author", "Flamingo Finance")]
    [ManifestExtra("Email", "developer@flamingo.finance")]
    [ManifestExtra("Description", "This is a Flamingo Contract")]
    [ContractPermission("*")]//avoid native contract hash change
    public class FlashLoan : SmartContract
    {
        [InitialValue("NaBUWGCLWFZTGK4V9f4pecuXmEijtGXMNX", ContractParameterType.Hash160)]
        private static readonly UInt160 InitialOwner;

        public static void onNEP17Payment(UInt160 from, BigInteger amount, byte[] data)
        {
            UInt160 tokenHash = Runtime.CallingScriptHash;
            BigInteger amountBack = amount * 1000 / 997 + 1;
            UInt160 @this = Runtime.ExecutingScriptHash;

#region Do whatever you want
            //just for test
            Contract.Call(tokenHash, "mint", CallFlags.All, new object[] { @this, @this, amount});
            BigInteger balanceOf = (BigInteger)Contract.Call(tokenHash, "balanceOf", CallFlags.All, new object[] { @this });
            Contract.Call(tokenHash, "transfer", CallFlags.All, new object[] { @this, InitialOwner, balanceOf - amountBack});
#endregion

            Contract.Call(tokenHash, "transfer", CallFlags.All, new object[] { @this, from, amountBack, null });
        }
    }
}
