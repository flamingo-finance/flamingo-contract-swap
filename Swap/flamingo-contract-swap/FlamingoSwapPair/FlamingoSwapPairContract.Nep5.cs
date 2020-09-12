using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace FlamingoSwapPair
{
    partial class FlamingoSwapPairContract
    {


        private const string BalanceMapKey = "AssetBalance";

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        [DisplayName("decimals")]
        public static byte Decimals() => 8;

        [DisplayName("name")]
        public static string Name() => "Exchange Pair"; //name of the token

        [DisplayName("symbol")]
        public static string Symbol() => "Temp-BC"; //symbol of the token


        [DisplayName("supportedStandards")]
        public static string[] SupportedStandards() => new string[] { "NEP-5", "NEP-7", "NEP-10" };

        [DisplayName("balanceOf")]
        public static BigInteger BalanceOf(byte[] account)
        {
            if (account.Length != 20) throw new Exception("The parameter account SHOULD be 20-byte addresses.");

            return Storage.CurrentContext.CreateMap(BalanceMapKey).Get(account).ToBigInteger();
        }

        private static bool SetBalance(byte[] account, BigInteger newBalance)
        {
            if (account.Length != 20) throw new Exception("The parameter account SHOULD be 20-byte addresses.");

            Storage.CurrentContext.CreateMap(BalanceMapKey).Put(account, newBalance);
            return true;
        }

        [DisplayName("totalSupply")]
        public static BigInteger GetTotalSupply()
        {
            return Storage.Get("totalSupply").ToBigInteger();
        }

        /// <summary>
        /// 修改发行量，增发或者销毁后时调用
        /// </summary>
        /// <param name="totalSupply"></param>
        /// <returns></returns>
        private static bool SetTotalSupply(BigInteger totalSupply)
        {
            Storage.Put("totalSupply", totalSupply);
            return true;
        }

        //Methods of actual execution
        private static bool Transfer(byte[] from, byte[] to, BigInteger amount, byte[] callscript)
        {
            //Check parameters
            Assert(from.Length == 20 && to.Length == 20, "The parameters from and to SHOULD be 20-byte addresses.");
            Assert(amount > 0, "The parameter amount MUST be greater than 0.");

            if (!Runtime.CheckWitness(from) && from.AsBigInteger() != callscript.AsBigInteger())
                return false;
            StorageMap asset = Storage.CurrentContext.CreateMap(BalanceMapKey);
            var fromAmount = asset.Get(from).AsBigInteger();
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
            var toAmount = asset.Get(to).AsBigInteger();
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
        private static void MintToken(byte[] toAddress, BigInteger amount)
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
        private static void BurnToken(byte[] fromAddress, BigInteger value)
        {
            SetBalance(fromAddress, BalanceOf(fromAddress) - value);
            SetTotalSupply(GetTotalSupply() - value);

            Transferred(fromAddress, new byte[20], value);
        }


        #endregion


    }
}
