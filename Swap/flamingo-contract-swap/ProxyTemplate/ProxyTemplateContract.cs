using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;

namespace ProxyTemplate
{
    [DisplayName("ProxyTemplate")]
    [ManifestExtra("Author", "NEO")]
    [ManifestExtra("Email", "developer@neo.org")]
    [ManifestExtra("Description", "This is a ProxyTemplate")]
    [ContractPermission("*")]
    public partial class ProxyTemplateContract : SmartContract
    {
        /// <summary>
        /// Deposit NEP17 token from owner to contract 
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="token"></param>
        /// <param name="amount"></param>
        public static void Deposit(UInt160 owner, UInt160 token, BigInteger amount)
        {
            // Global
            // Check sender and parameters
            Assert(owner.IsValid && token.IsValid && amount > 0, "Invalid Parameters");
            Assert(Runtime.CheckWitness(owner), "Forbidden");
            Assert(token == Token0 || token == Token1, "Unsupported Token");

            // Transfer
            SafeTransfer(token, owner, Runtime.ExecutingScriptHash, amount);

            // Mint yToken
            YMint(token, owner, amount);
            onDeposit(token, owner, amount);
        }

        /// <summary>
        /// Withdraw NEP17 token from contract to owner 
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="token"></param>
        /// <param name="amount"></param>
        public static void Withdraw(UInt160 owner, UInt160 token, BigInteger amount)
        {
            // CalledByEntry
            // Check owner and parameters
            Assert(owner.IsValid && token.IsValid && amount > 0, "Invalid Parameters");
            Assert(Runtime.CheckWitness(owner), "Forbidden");
            Assert(token == Token0 || token == Token1, "Unsupported Token");

            // Check balance
            Assert(DepositOf(token, owner) >= amount, "Insufficient Balance");

            // Burn yToken
            YBurn(token, owner, amount);

            // Transfer
            SafeTransfer(token, Runtime.ExecutingScriptHash, owner, amount);
            onWithdraw(token, owner, amount);
        }

        /// <summary>
        /// Add liquidity with deposited tokens and receive LP token
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="amount0Desired">Desired input amount of Token0</param>
        /// <param name="amount1Desired">Desired input amount of Token1</param>
        /// <param name="amount0Min">Minimal input amount of Token0</param>
        /// <param name="amount1Min">Minimal input amount of Token1</param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static BigInteger[] ProxyAddLiquidity(UInt160 owner, BigInteger amount0Desired, BigInteger amount1Desired, BigInteger amount0Min, BigInteger amount1Min, BigInteger deadLine)
        {
            // CalledByEntry
            // Check sender and parameters
            Assert(owner.IsValid && amount0Desired >= 0 && amount1Desired >= 0 && amount0Min >= 0 && amount1Min >= 0 && deadLine > 0, "Invalid Parameters");
            Assert(Runtime.CheckWitness(owner), "Forbidden");

            // Record balance
            UInt160 me = Runtime.ExecutingScriptHash;
            BigInteger balance0Before = (BigInteger)Contract.Call(Token0, "balanceOf", CallFlags.ReadOnly, new object[] { me });
            BigInteger balance1Before = (BigInteger)Contract.Call(Token1, "balanceOf", CallFlags.ReadOnly, new object[] { me });
            BigInteger balanceLPBefore = (BigInteger)Contract.Call(Pair01, "balanceOf", CallFlags.ReadOnly, new object[] { me });

            // Approve transfer
            Assert(DepositOf(Token0, owner) >= amount0Desired && DepositOf(Token1, owner) >= amount1Desired, "Insufficient Balance");
            Approve(Token0, owner, Pair01, amount0Desired);
            Approve(Token1, owner, Pair01, amount1Desired);

            // Add liquidity
            var result = Contract.Call(Router, "addLiquidity", CallFlags.All, new object[] { Token0, Token1, amount0Desired, amount1Desired, amount0Min, amount1Min, deadLine });

            // Retrieve allowance
            Retrieve(Token0, Pair01);
            Retrieve(Token1, Pair01);

            // Get balance again
            BigInteger balance0After = (BigInteger)Contract.Call(Token0, "balanceOf", CallFlags.ReadOnly, new object[] { me });
            BigInteger balance1After = (BigInteger)Contract.Call(Token1, "balanceOf", CallFlags.ReadOnly, new object[] { me });
            BigInteger balanceLPAfter = (BigInteger)Contract.Call(Pair01, "balanceOf", CallFlags.ReadOnly, new object[] { me });
            onAdd(balance0Before - balance0After, balance1Before - balance1After);

            // Mint yLPToken
            YBurn(Token0, owner, balance0Before - balance0After);
            YBurn(Token1, owner, balance1Before - balance1After);
            YMint(Pair01, owner, balanceLPAfter - balanceLPBefore);

            return (BigInteger[])result;
        }

        /// <summary>
        /// Remove liquidity with deposited LP token and receive Token0, Token1
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="liquidity">Input amount of LP token</param>
        /// <param name="amount0Min">Minimal output amount of Token0</param>
        /// <param name="amount1Min">Minimal output amount of Token1</param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static BigInteger[] ProxyRemoveLiquidity(UInt160 owner, BigInteger liquidity, BigInteger amount0Min, BigInteger amount1Min, BigInteger deadLine)
        {
            // CalledByEntry
            // Check sender and parameters
            Assert(owner.IsValid && liquidity > 0 && amount0Min >= 0 && amount1Min >= 0 && deadLine > 0, "Invalid Parameters");
            Assert(Runtime.CheckWitness(owner), "Forbidden");

            // Record balance
            UInt160 me = Runtime.ExecutingScriptHash;
            BigInteger balance0Before = (BigInteger)Contract.Call(Token0, "balanceOf", CallFlags.ReadOnly, new object[] { me });
            BigInteger balance1Before = (BigInteger)Contract.Call(Token1, "balanceOf", CallFlags.ReadOnly, new object[] { me });

            // Approve transfer
            Assert(DepositOf(Pair01, owner) >= liquidity, "Insufficient Balance");
            Approve(Pair01, owner, Router, liquidity);

            // Remove liquidity
            var result = Contract.Call(Router, "removeLiquidity", CallFlags.All, new object[] { Token0, Token1, liquidity, amount0Min, amount1Min, deadLine });

            // Retrieve allowance
            Retrieve(Pair01, Router);

            // Get balance again
            BigInteger balance0After = (BigInteger)Contract.Call(Token0, "balanceOf", CallFlags.ReadOnly, new object[] { me });
            BigInteger balance1After = (BigInteger)Contract.Call(Token1, "balanceOf", CallFlags.ReadOnly, new object[] { me });
            onRemove(balance0After - balance0Before, balance1After - balance1Before);

            // Burn yLPToken
            YBurn(Pair01, owner, liquidity);
            YMint(Token0, owner, balance0After - balance0Before);
            YMint(Token1, owner, balance1After - balance1Before);

            return (BigInteger[])result;
        }

        /// <summary>
        /// Swap deposited tokens with a fixed input amount
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="amountIn">Input amount of deposited token</param>
        /// <param name="amountOutMin">Minimal amount of the output</param>
        /// <param name="isToken0to1">If swap from Token0 to Token1</param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static bool ProxySwapTokenInForTokenOut(UInt160 sender, BigInteger amountIn, BigInteger amountOutMin, bool isToken0to1, BigInteger deadLine)
        {
            // CalledByEntry
            // Check sender and parameters
            Assert(sender.IsValid && amountIn > 0 && amountOutMin >= 0 && deadLine > 0, "Invalid Parameters");
            Assert(Runtime.CheckWitness(sender), "Forbidden");

            // Record balance
            UInt160 me = Runtime.ExecutingScriptHash;
            UInt160[] path = isToken0to1 ? new UInt160[] { Token0, Token1 } : new UInt160[] { Token1, Token0 };
            UInt160 unpredictableSpent = isToken0to1 ? Token1 : Token0;
            BigInteger balanceBefore = (BigInteger)Contract.Call(unpredictableSpent, "balanceOf", CallFlags.ReadOnly, new object[] { me });

            // Approve transfer
            Assert(DepositOf(Pair01, sender) >= amountIn, "Insufficient Balance");
            Approve(path[0], sender, Pair01, amountIn);

            // Swap in for out
            var result = Contract.Call(Router, "swapTokenInForTokenOut", CallFlags.All, new object[] { amountIn, amountOutMin, path, deadLine });

            // Retrieve allowance
            Retrieve(path[0], Pair01);

            // Get balance again
            BigInteger balanceAfter = (BigInteger)Contract.Call(unpredictableSpent, "balanceOf", CallFlags.ReadOnly, new object[] { me });

            // Burn yToken
            YBurn(path[0], sender, amountIn);
            YMint(path[1], sender, balanceAfter - balanceBefore);

            return (bool)result;
        }

        /// <summary>
        /// Swap deposited tokens with a fixed output amount
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="amountOut">Output amount of deposited token</param>
        /// <param name="amountInMax">Maximal amount of the input</param>
        /// <param name="isToken0to1">If swap from Token0 to Token1</param>
        /// <param name="deadLine"></param>
        /// <returns></returns>
        public static bool ProxySwapTokenOutForTokenIn(UInt160 sender, BigInteger amountOut, BigInteger amountInMax, bool isToken0to1, BigInteger deadLine)
        {
            // CalledByEntry
            // Check sender and parameters
            Assert(sender.IsValid && amountOut > 0 && amountInMax >= 0 && deadLine > 0, "Invalid Parameters");
            Assert(Runtime.CheckWitness(sender), "Forbidden");

            // Record balance
            UInt160 me = Runtime.ExecutingScriptHash;
            UInt160[] path = isToken0to1 ? new UInt160[] { Token0, Token1 } : new UInt160[] { Token1, Token0 };
            UInt160 unpredictableSpent = isToken0to1 ? Token0 : Token1;
            BigInteger balanceBefore = (BigInteger)Contract.Call(unpredictableSpent, "balanceOf", CallFlags.ReadOnly, new object[] { me });

            // Approve transfer
            Assert(DepositOf(path[0], sender) >= amountInMax, "Insufficient Balance");
            Approve(path[0], sender, Pair01, amountInMax);

            // Swap in for out
            var result = Contract.Call(Router, "swapTokenOutForTokenIn", CallFlags.All, new object[] { amountOut, amountInMax, path, deadLine });

            // Retrieve allowance
            Retrieve(path[0], Pair01);

            // Get balance again
            BigInteger balanceAfter = (BigInteger)Contract.Call(unpredictableSpent, "balanceOf", CallFlags.ReadOnly, new object[] { me });

            // Burn yToken
            YBurn(path[0], sender, balanceBefore - balanceAfter);
            YMint(path[1], sender, amountOut);

            return (bool)result;
        }
    }
}
