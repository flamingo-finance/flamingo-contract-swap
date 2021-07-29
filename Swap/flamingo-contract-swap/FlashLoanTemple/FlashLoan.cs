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
        [InitialValue("NVWZmsKJaiKNdKMg6yQPPCNHP4GDZUELJW", ContractParameterType.Hash160)]
        private static readonly UInt160 InitialOwner;

        public delegate void Notify(params object[] arg);
        [DisplayName("event_name")]
        public static event Notify OnNotify;

        public static bool Verify()
        {
            return Runtime.CheckWitness(InitialOwner);
        }

        public static void onNEP17Payment(UInt160 from, BigInteger amount, BigInteger data)
        {
            UInt160 tokenHash = Runtime.CallingScriptHash;
            if (!data.Equals(123)) return;

            BigInteger amountBack = amount * 1000 / 997 + 1;

            UInt160 @this = Runtime.ExecutingScriptHash;

            #region Do whatever you want
            //just for test

            BigInteger balanceOf = (BigInteger)Contract.Call(tokenHash, "balanceOf", CallFlags.All, new object[] { @this });
            OnNotify(data);
            OnNotify("this is operation of flash loan...");
            OnNotify(balanceOf);

            Contract.Call(tokenHash, "transfer", CallFlags.All, new object[] { @this, InitialOwner, balanceOf - amountBack, data});
            #endregion

            Contract.Call(tokenHash, "transfer", CallFlags.All, new object[] { @this, from, amountBack, data });
        }
    }
}
