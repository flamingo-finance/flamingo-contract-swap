using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;

namespace FlamingoSwapPair
{
    partial class FlamingoSwapPairContract
    {


        private const string BalanceMapKey = "AssetBalance";

        [DisplayName("transfer")]
        public static event Action<UInt160, UInt160, BigInteger> Transferred;

        [DisplayName("decimals")]
        public static byte Decimals() => 8;

        [DisplayName("name")]
        public static string Name() => "Exchange Pair"; //name of the token



        [DisplayName("supportedStandards")]
        public static string[] SupportedStandards() => new string[] { "NEP-5", "NEP-7", "NEP-10" };

        [DisplayName("balanceOf")]
        public static BigInteger BalanceOf(UInt160 account)
        {
            //if (account.Length != 20) throw new Exception("The parameter account SHOULD be 20-byte addresses.");

            return ((byte[])Storage.CurrentContext.CreateMap(BalanceMapKey).Get(account)).ToBigInteger();
        }

        private static bool SetBalance(UInt160 account, BigInteger newBalance)
        {
            Storage.CurrentContext.CreateMap(BalanceMapKey).Put(account, newBalance);
            return true;
        }

        [DisplayName("totalSupply")]
        public static BigInteger GetTotalSupply()
        {
            return ((byte[])StorageGet("totalSupply")).ToBigInteger();
        }

        /// <summary>
        /// 修改发行量，增发或者销毁后时调用
        /// </summary>
        /// <param name="totalSupply"></param>
        /// <returns></returns>
        private static bool SetTotalSupply(BigInteger totalSupply)
        {
            StoragePut("totalSupply", totalSupply);
            return true;
        }

        //Methods of actual execution
        private static bool Transfer(UInt160 from, UInt160 to, BigInteger amount, UInt160 callscript)
        {
            //Check parameters
            //Assert(from.Length == 20 && to.Length == 20, "The parameters from and to SHOULD be 20-byte addresses.");
            Assert(amount >= 0, "The parameter amount MUST be greater than 0.");

            var me = ExecutionEngine.ExecutingScriptHash;
            if (to == me)
            {
                Assert(CheckIsRouter(callscript), "Only support transfer to me by Router");
            }

            if (!Runtime.CheckWitness(from) && from != callscript)
                return false;
            StorageMap asset = Storage.CurrentContext.CreateMap(BalanceMapKey);
            var fromAmount = ((byte[])asset.Get(from)).ToBigInteger();
            if (fromAmount < amount)
                return false;
            if (from == to)
                return true;


            //Reduce payer balances
            if (fromAmount == amount)
                asset.Delete(from);
            else
                asset.Put(from, fromAmount - amount);

            //Increase the payee balance
            var toAmount = ((byte[])asset.Get(to)).ToBigInteger();
            asset.Put(to, toAmount + amount);

            Transferred(from, to, amount);
            return true;
        }


        #region 非标准方法



        /// <summary>
        /// 铸币（不校验签名），内部方法禁止外部直接调用
        /// </summary>
        /// <param name="toAddress">接收新铸造的币的账号</param>
        /// <param name="amount">铸造量</param>
        private static void MintToken(UInt160 toAddress, BigInteger amount)
        {
            SetBalance(toAddress, BalanceOf(toAddress) + amount);
            SetTotalSupply(GetTotalSupply() + amount);

            Transferred(null, toAddress, amount);
        }

        /// <summary>
        /// 物理销毁token（不校验签名），内部方法禁止外部直接调用
        /// </summary>
        /// <param name="fromAddress">token的持有地址</param>
        /// <param name="value">销毁的token量</param>
        private static void BurnToken(UInt160 fromAddress, BigInteger value)
        {
            SetBalance(fromAddress, BalanceOf(fromAddress) - value);
            SetTotalSupply(GetTotalSupply() - value);

            Transferred(fromAddress, UInt160.Zero, value);
        }


        #endregion


    }
}
