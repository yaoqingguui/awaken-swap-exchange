using System.Linq;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Types;
using Awaken.Contracts.Swap;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using ApproveInput = Awaken.Contracts.Token.ApproveInput;
using TransferFromInput = Awaken.Contracts.Token.TransferFromInput;

namespace Awaken.Contracts.SwapExchangeContract
{
    /// <summary>
    /// The C# implementation of the contract defined in swap_exchange_contract.proto that is located in the "protobuf"
    /// folder.
    /// Notice that it inherits from the protobuf generated code. 
    /// </summary>
    public partial class SwapExchangeContract
    {
        /**
         * SetTargetToken
         */
        public override Empty SetTargetToken(StringValue input)
        {
            OnlyOwner();
            var tokenInfo = State.CommonTokenContract.GetTokenInfo.Call(new GetTokenInfoInput
            {
                Symbol = input.Value
            });
            Assert(tokenInfo != null && tokenInfo.Symbol.Equals(input.Value), $"Token {input.Value} not exist.");
            State.TargetToken.Value = input.Value;
            return new Empty();
        }

        /**
         * SetReceivor
         */
        public override Empty SetReceivor(Address input)
        {
            OnlyOwner();
            Assert(input != null, "Invalid input.");
            State.Receivor.Value = input;
            return new Empty();
        }

        /**
         * SwapCommonTokens
         */
        public override Empty SwapCommonTokens(SwapTokensInput input)
        {
            OnlyOwner();
            var tokensInfo = input.SwapTokenList.TokensInfo;
            Assert(tokensInfo.Count > 0, "Invalid params.");
            State.CumulativeTokenList.Value = new TokenList
            {
                TokensInfo = {tokensInfo}
            };
            
            //transfer in
            foreach (var token in State.CumulativeTokenList.Value.TokensInfo)
            {
                State.CommonTokenContract.TransferFrom.Send(new AElf.Contracts.MultiToken.TransferFromInput
                {
                    Amount = token.Amount,
                    From = Context.Sender,
                    Symbol = token.TokenSymbol,
                    To = Context.Self
                });
            }
            
            Context.SendInline(Context.Self, nameof(SwapTokensInline), new SwapTokensInlineInput
            {
                PathMap = {input.PathMap}
            });
            return new Empty();
        }

        /**
         * SwapLpTokens
         */
        public override Empty SwapLpTokens(SwapTokensInput input)
        {
            OnlyOwner();
            var tokensInfo = input.SwapTokenList.TokensInfo;
            Assert(tokensInfo.Count > 0, "Invalid params.");
            State.CumulativeTokenList.Value = new TokenList();
            foreach (var token in tokensInfo)
            {
                State.LpTokenContract.TransferFrom.Send(new TransferFromInput
                {
                    From = Context.Sender,
                    Amount = token.Amount,
                    Symbol = token.TokenSymbol,
                    To = Context.Self
                });

                Context.SendInline(Context.Self, nameof(RemoveLiquidityInline), new RemoveLiquidityInlineInput
                {
                    Token = token
                });
            }

            Context.SendInline(Context.Self, nameof(SwapTokensInline), new SwapTokensInlineInput
            {
                PathMap = {input.PathMap}
            });

            return new Empty();
        }

        /**
         * RemoveLiquidityInline
         */
        public override Empty RemoveLiquidityInline(RemoveLiquidityInlineInput input)
        {
            OnlySelf();
            State.LpTokenContract.Approve.Send(new ApproveInput
            {
                Spender = State.SwapContract.Value,
                Amount = input.Token.Amount,
                Symbol = input.Token.TokenSymbol
            });

            var tokens = ExtractTokensFromTokenPair(ExtractTokenPairFromSymbol(input.Token.TokenSymbol));
            var tokenABalanceBefore = State.CommonTokenContract.GetBalance.Call(new GetBalanceInput
            {
                Owner = Context.Self,
                Symbol = tokens[0]
            }).Balance;
            var tokenBBalanceBefore = State.CommonTokenContract.GetBalance.Call(new GetBalanceInput
            {
                Owner = Context.Self,
                Symbol = tokens[1]
            }).Balance;

            State.SwapContract.RemoveLiquidity.Send(new RemoveLiquidityInput
            {
                To = Context.Self,
                LiquidityRemove = input.Token.Amount,
                SymbolA = tokens[0],
                SymbolB = tokens[1],
                AmountAMin = 1,
                AmountBMin = 1,
                Deadline = Context.CurrentBlockTime.AddSeconds(3)
            });

            Context.SendInline(Context.Self, nameof(CumulativeTokenAmountInline), new CumulativeTokenAmountInlineInput
            {
                TokenA = tokens[0],
                TokenB = tokens[1],
                TokenABefore = tokenABalanceBefore,
                TokenBBefore = tokenBBalanceBefore
            });
            return new Empty();
        }

        /**
         * CumulativeTokenAmountInline
         */
        public override Empty CumulativeTokenAmountInline(CumulativeTokenAmountInlineInput input)
        {
            OnlySelf();
            var tokenABalanceAfter = State.CommonTokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = input.TokenA,
                Owner = Context.Self
            }).Balance;

            var tokenBBalanceAfter = State.CommonTokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = input.TokenB,
                Owner = Context.Self
            }).Balance;
            var increaseAmountTokenA = tokenABalanceAfter.Sub(input.TokenABefore);
            var increaseAmountTokenB = tokenBBalanceAfter.Sub(input.TokenBBefore);
            var tokenList = State.CumulativeTokenList.Value;
            var tokenA = tokenList.TokensInfo.FirstOrDefault(token => token.TokenSymbol.Equals(input.TokenA));
            if (tokenA != null)
            {
                tokenA.Amount = tokenA.Amount.Add(increaseAmountTokenA);
            }
            else
            {
                tokenList.TokensInfo.Add(new Token
                {
                    TokenSymbol = input.TokenA,
                    Amount = increaseAmountTokenA
                });
            }

            var tokenB = tokenList.TokensInfo.FirstOrDefault(token => token.TokenSymbol.Equals(input.TokenB));

            if (tokenB != null)
            {
                tokenB.Amount = tokenB.Amount.Add(increaseAmountTokenB);
            }
            else
            {
                tokenList.TokensInfo.Add(new Token
                {
                    TokenSymbol = input.TokenB,
                    Amount = increaseAmountTokenB
                });
            }

            return new Empty();
        }

        /**
         * SwapTokensInline
         */
        public override Empty SwapTokensInline(SwapTokensInlineInput input)
        {
            OnlySelf();
            var pathMap = input.PathMap;
            var tokensInfo = State.CumulativeTokenList.Value.TokensInfo;
            Assert(tokensInfo.Count > 0, "Invalid cumulative token list.");
            foreach (var token in tokensInfo)
            {
                if (State.TargetToken.Value.Equals(token.TokenSymbol)&&token.Amount>0)
                {
                    TransferTargetTokenToReceiver(token);
                    continue;
                }
                var path = pathMap[token.TokenSymbol];
                Assert(path != null && path.Value.Count > 0, $"{token} path lose.");
                SwapTokenToTarget(token, path);
            }
            return new Empty();
        }

        private void TransferTargetTokenToReceiver(Token token)
        {
           State.CommonTokenContract.Transfer.Send(new TransferInput
           {
               Amount = token.Amount,
               Symbol = token.TokenSymbol,
               To = State.Receivor.Value
           });
        }

        private void SwapTokenToTarget(Token token, Path pathPair)
        {
            var path = new RepeatedField<string> {token.TokenSymbol};
            if (pathPair.Value[0].Contains("-"))
            {
                foreach (var pair in pathPair.Value)
                {
                    var tokens = ExtractTokensFromTokenPair(ExtractTokenPairFromSymbol(pair));
                    path.Add(tokens[0].Equals(path[path.Count - 1]) ? tokens[1] : tokens[0]);
                }
            }
            else
            {
                path = pathPair.Value;
            }

            State.CommonTokenContract.Approve.Send(new AElf.Contracts.MultiToken.ApproveInput
            {
                Spender = State.SwapContract.Value,
                Amount = token.Amount,
                Symbol = token.TokenSymbol
            });

            State.SwapContract.SwapExactTokensForTokens.Send(new SwapExactTokensForTokensInput
            {
                Path = {path},
                Channel = "Dividend pool script",
                To = State.Receivor.Value,
                AmountIn = token.Amount,
                AmountOutMin = 1,
                Deadline = Context.CurrentBlockTime.AddSeconds(3)
            });
        }
    }
}