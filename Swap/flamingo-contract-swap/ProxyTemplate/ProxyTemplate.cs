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
    public partial class ProxyTemplate : SmartContract
    {
        public static void Deposit(UInt160 from, UInt160 token, BigInteger amount)
        {
            // Global
            // Check sender and token address
            Assert(Runtime.CheckWitness(from), "Forbidden");
            Assert(token == Token0 || token == Token1, "Unsupported Token");

            // Transfer
            UInt160 me = Runtime.ExecutingScriptHash;
            var result = (bool)Contract.Call(token, "transfer", CallFlags.All, new object[] { from, me, amount, null });
            Assert(result, "Transfer Fail", token);

            // Mint yToken
            YMint(token, from, amount);
            onDeposit(token, from, amount);
        }

        public static void Withdraw(UInt160 to, UInt160 token, BigInteger amount)
        {
            // CalledByEntry
            // Check owner and token address
            Assert(Runtime.CheckWitness(to), "Forbidden");
            Assert(token == Token0 || token == Token1, "Unsupported Token");

            // Check balance
            Assert(DepositOf(token, to) >= amount, "Insufficient Balance");

            // Burn yToken
            YBurn(token, to, amount);

            // Transfer
            UInt160 me = Runtime.ExecutingScriptHash;
            var result = (bool)Contract.Call(token, "transfer", CallFlags.All, new object[] { me, to, amount, null });
            Assert(result, "Transfer Fail", token);
            onWithdraw(token, to, amount);
        }

        public static BigInteger[] ProxyAddLiquidity(UInt160 sender, BigInteger amount0Desired, BigInteger amount1Desired, BigInteger amount0Min, BigInteger amount1Min, BigInteger deadLine)
        {
            // CalledByEntry
            // Check sender
            Assert(Runtime.CheckWitness(sender), "Forbidden");

            // Record balance
            UInt160 me = Runtime.ExecutingScriptHash;
            BigInteger balance0Before = (BigInteger)Contract.Call(Token0, "balanceOf", CallFlags.All, new object[] { me });
            BigInteger balance1Before = (BigInteger)Contract.Call(Token1, "balanceOf", CallFlags.All, new object[] { me });
            BigInteger balanceLPBefore = (BigInteger)Contract.Call(Pair01, "balanceOf", CallFlags.All, new object[] { me });

            // Approve transfer
            Assert(amount0Desired >= 0 && amount1Desired >= 0, "Insufficient parameters");
            Assert(Approve(Token0, sender, Pair01, amount0Desired), "Insufficient Token0 balance");
            onApprove(Token0, Pair01, amount0Desired);
            Assert(Approve(Token1, sender, Pair01, amount1Desired), "Insufficient Token1 balance");
            onApprove(Token1, Pair01, amount1Desired);

            // Add liquidity
            var result = Contract.Call(Router, "addLiquidity", CallFlags.All, new object[] { me, Token0, Token1, amount0Desired, amount1Desired, amount0Min, amount1Min, deadLine });

            // Retrieve allowance
            Retrieve(Token0, Pair01);
            onRetrieve(Token0, Pair01);
            Retrieve(Token1, Pair01);
            onRetrieve(Token1, Pair01);

            // Get balance again
            BigInteger balance0After = (BigInteger)Contract.Call(Token0, "balanceOf", CallFlags.All, new object[] { me });
            BigInteger balance1After = (BigInteger)Contract.Call(Token1, "balanceOf", CallFlags.All, new object[] { me });
            BigInteger balanceLPAfter = (BigInteger)Contract.Call(Pair01, "balanceOf", CallFlags.All, new object[] { me });
            onAdd(balance0Before - balance0After, balance1Before - balance1After);

            // Mint yLPToken
            YBurn(Token0, sender, balance0Before - balance0After);
            YBurn(Token1, sender, balance1Before - balance1After);
            YMint(Pair01, sender, balanceLPAfter - balanceLPBefore);

            return (BigInteger[])result;
        }

        public static BigInteger[] ProxyRemoveLiquidity(UInt160 sender, BigInteger liquidity, BigInteger amount0Min, BigInteger amount1Min, BigInteger deadLine)
        {
            // CalledByEntry
            // Check sender
            Assert(Runtime.CheckWitness(sender), "Forbidden");

            // Record balance
            UInt160 me = Runtime.ExecutingScriptHash;
            BigInteger balance0Before = (BigInteger)Contract.Call(Token0, "balanceOf", CallFlags.All, new object[] { me });
            BigInteger balance1Before = (BigInteger)Contract.Call(Token1, "balanceOf", CallFlags.All, new object[] { me });
            BigInteger balanceLPBefore = (BigInteger)Contract.Call(Pair01, "balanceOf", CallFlags.All, new object[] { me });

            // Approve transfer
            Assert(liquidity >= 0, "Insufficient parameters");
            Assert(Approve(Pair01, sender, Pair01, liquidity), "Insufficient LPToken balance");
            onApprove(Pair01, Pair01, liquidity);

            // Remove liquidity
            var result = Contract.Call(Router, "removeLiquidity", CallFlags.All, new object[] { me, Token0, Token1, liquidity, amount0Min, amount1Min, deadLine });

            // Retrieve allowance
            Retrieve(Pair01, Pair01);
            onRetrieve(Pair01, Pair01);

            // Get balance again
            BigInteger balance0After = (BigInteger)Contract.Call(Token0, "balanceOf", CallFlags.All, new object[] { me });
            BigInteger balance1After = (BigInteger)Contract.Call(Token1, "balanceOf", CallFlags.All, new object[] { me });
            BigInteger balanceLPAfter = (BigInteger)Contract.Call(Pair01, "balanceOf", CallFlags.All, new object[] { me });
            onRemove(balance0After - balance0Before, balance1After - balance1Before);

            // Burn yLPToken
            YBurn(Pair01, sender, balanceLPBefore - balanceLPAfter);
            YMint(Token0, sender, balance0After - balance0Before);
            YMint(Token1, sender, balance1After - balance1Before);

            return (BigInteger[])result;
        }

        public static bool ProxySwapTokenInForTokenOut(UInt160 sender, BigInteger amountIn, BigInteger amountOutMin, bool isToken0to1, BigInteger deadLine)
        {
            // CalledByEntry
            // Check sender
            Assert(Runtime.CheckWitness(sender), "Forbidden");

            // Record balance
            UInt160 me = Runtime.ExecutingScriptHash;
            UInt160[] path = isToken0to1 ? new UInt160[] { Token0, Token1 } : new UInt160[] { Token1, Token1 };
            UInt160 unpredictableSpent = isToken0to1 ? Token1 : Token0;
            BigInteger balanceBefore = (BigInteger)Contract.Call(unpredictableSpent, "balanceOf", CallFlags.All, new object[] { me });

            // Approve transfer
            Assert(amountIn >= 0, "Insufficient parameters");
            Assert(Approve(path[0], sender, Pair01, amountIn), "Insufficient Token balance");
            onApprove(path[0], Pair01, amountIn);

            // Swap in for out
            var result = Contract.Call(Router, "swapTokenInForTokenOut", CallFlags.All, new object[] { me, amountIn, amountOutMin, path, deadLine });

            // Retrieve allowance
            Retrieve(path[0], Pair01);
            onRetrieve(path[0], Pair01);

            // Get balance again
            BigInteger balanceAfter = (BigInteger)Contract.Call(unpredictableSpent, "balanceOf", CallFlags.All, new object[] { me });

            // Burn yToken
            YBurn(path[0], sender, amountIn);
            YMint(path[1], sender, balanceAfter - balanceBefore);

            return (bool)result;
        }

        public static bool ProxySwapTokenOutForTokenIn(UInt160 sender, BigInteger amountOut, BigInteger amountInMax, bool isToken0to1, BigInteger deadLine)
        {
            // CalledByEntry
            // Check sender
            Assert(Runtime.CheckWitness(sender), "Forbidden");

            // Record balance
            UInt160 me = Runtime.ExecutingScriptHash;
            UInt160[] path = isToken0to1 ? new UInt160[] { Token0, Token1 } : new UInt160[] { Token1, Token1 };
            UInt160 unpredictableSpent = isToken0to1 ? Token0 : Token1;
            BigInteger balanceBefore = (BigInteger)Contract.Call(unpredictableSpent, "balanceOf", CallFlags.All, new object[] { me });

            // Approve transfer
            Assert(amountOut >= 0, "Insufficient parameters");
            Assert(Approve(path[0], sender, Pair01, amountInMax), "Insufficient Token balance");
            onApprove(path[0], Pair01, amountInMax);

            // Swap in for out
            var result = Contract.Call(Router, "swapTokenOutForTokenIn", CallFlags.All, new object[] { me, amountOut, amountInMax, path, deadLine });

            // Retrieve allowance
            Retrieve(path[0], Pair01);
            onRetrieve(path[0], Pair01);

            // Get balance again
            BigInteger balanceAfter = (BigInteger)Contract.Call(unpredictableSpent, "balanceOf", CallFlags.All, new object[] { me });

            // Burn yToken
            YBurn(path[0], sender, balanceBefore - balanceAfter);
            YMint(path[1], sender, amountOut);

            return (bool)result;
        }
    }
}
