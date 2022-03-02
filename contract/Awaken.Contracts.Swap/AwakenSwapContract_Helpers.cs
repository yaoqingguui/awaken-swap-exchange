using System;
using System.Linq;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Awaken.Contracts.Token;
using Google.Protobuf.Collections;
using GetBalanceInput = AElf.Contracts.MultiToken.GetBalanceInput;
using GetTokenInfoInput = AElf.Contracts.MultiToken.GetTokenInfoInput;
using TransferFromInput = AElf.Contracts.MultiToken.TransferFromInput;

namespace Awaken.Contracts.Swap
{
    public partial class AwakenSwapContract
    {
        private string GetTokenPair(string tokenA, string tokenB)
        {
            var symbols = SortSymbols(tokenA, tokenB);
            return $"{symbols[0]}-{symbols[1]}";
        }

        private string GetTokenPairSymbol(string tokenA, string tokenB)
        {
            var symbols = SortSymbols(tokenA, tokenB);
            return $"ALP {symbols[0]}-{symbols[1]}";
        }

        private string[] SortSymbols(params string[] symbols)
        {
            Assert(symbols.Length == 2, "Invalid symbols for sorting.");
            return symbols.OrderBy(s => s).ToArray();
        }

        /// <summary>
        /// Extract "ABC" & "DEF" from "ABC-DEF",
        /// and ranked.
        /// </summary>
        /// <param name="tokenPair"></param>
        /// <returns></returns>
        private string[] ExtractTokenPair(string tokenPair)
        {
            Assert(tokenPair.Contains("-") && tokenPair.Count(c => c == '-') == 1, $"Invalid TokenPair {tokenPair}.");
            return SortSymbols(tokenPair.Split('-'));
        }

        private void ValidTokenSymbol(string token)
        {
            var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
            {
                Symbol = token
            });
            Assert(!string.IsNullOrEmpty(tokenInfo.Symbol), $"Token {token} not exists.");
        }

        private void ValidPairSymbol(string tokenA, string tokenB)
        {
            Assert(State.PairVirtualAddressMap[tokenA][tokenB] != null, $"Pair {tokenA}-{tokenB} does not exist.");
        }

        private long[] AddLiquidity(string tokenA, string tokenB, long amountADesired, long amountBDesired,
            long amountAMin, long amountBMin)
        {
            // The sorting only for getting pair virtual address.
            var symbols = SortSymbols(tokenA, tokenB);
            ValidPairSymbol(symbols[0], symbols[1]);
            var pairVirtualAddress = State.PairVirtualAddressMap[symbols[0]][symbols[1]];

            long amountA;
            long amountB;
            var reserves = GetReserves(pairVirtualAddress, tokenA, tokenB);
            if (reserves[0] == 0 && reserves[1] == 0)
            {
                // First time to add liquidity.
                amountA = amountADesired;
                amountB = amountBDesired;
            }
            else
            {
                // Not the first time, need to consider the changes of liquidity pool. 
                var amountBOptimal = Quote(amountADesired, reserves[0], reserves[1]);
                if (amountBOptimal <= amountBDesired)
                {
                    Assert(amountBOptimal >= amountBMin, $"Insufficient amount of token {tokenB}.");
                    amountA = amountADesired;
                    amountB = amountBOptimal;
                }
                else
                {
                    var amountAOptimal = Quote(amountBDesired, reserves[1], reserves[0]);
                    Assert(amountAOptimal <= amountADesired);
                    Assert(amountAOptimal >= amountAMin, $"Insufficient amount of token {tokenA}.");
                    amountA = amountAOptimal;
                    amountB = amountBDesired;
                }
            }

            return new[]
            {
                amountA, amountB
            };
        }

        private long[] RemoveLiquidity(string tokenA, string tokenB, long liquidityRemoveAmount,
            long amountAMin, long amountBMin, Address to)
        {
            var symbols = SortSymbols(tokenA, tokenB);
            ValidPairSymbol(symbols[0], symbols[1]);
            var liquidity = State.LPTokenContract.GetBalance.Call(new Awaken.Contracts.Token.GetBalanceInput
            {
                Symbol = GetTokenPairSymbol(tokenA, tokenB),
                Owner = Context.Sender
            }).Amount;
            Assert(liquidity > 0 && liquidityRemoveAmount <= liquidity, "Insufficient LiquidityToken");
            var amountList = PerformBurn(to, tokenA, tokenB, liquidityRemoveAmount);
            Assert(amountList[0] >= amountAMin, $"Insufficient token {tokenA}.");
            Assert(amountList[1] >= amountBMin, $"Insufficient token {tokenB}.");
            return new[]
            {
                amountList[0], amountList[1]
            };
        }

        private long[] GetReserves(Address pairAddress, string tokenA, string tokenB)
        {
            var amountA = State.TotalReservesMap[pairAddress][tokenA];
            var amountB = State.TotalReservesMap[pairAddress][tokenB];
            return new[]
            {
                amountA, amountB
            };
        }

        private Address GetPairAddress(string tokenA, string tokenB)
        {
            var symbols = SortSymbols(tokenA, tokenB);
            var pairAddress = State.PairVirtualAddressMap[symbols[0]][symbols[1]];
            return pairAddress;
        }

        // ReSharper disable once InconsistentNaming
        private long MintLPToken(string tokenA, string tokenB, long amountA, long amountB, Address account,
            string channel)
        {
            var pairAddress = GetPairAddress(tokenA, tokenB);
            var balanceA = State.PoolBalanceMap[pairAddress][tokenA];
            var balanceB = State.PoolBalanceMap[pairAddress][tokenB];

            var reserves = GetReserves(pairAddress, tokenA, tokenB);
            var feeOn = MintFee(reserves[0], reserves[1], pairAddress, tokenA, tokenB);
            var lpTokenSymbol = GetTokenPairSymbol(tokenA, tokenB);
            var lpTokenInfo = State.LPTokenContract.GetTokenInfo.Call(new Awaken.Contracts.Token.GetTokenInfoInput
            {
                Symbol = lpTokenSymbol
            });
            var liquidity = lpTokenInfo.Supply == 0
                ? Convert.ToInt64(Sqrt(new BigIntValue(amountA).Mul(amountB).Sub(1)).Value)
                : Math.Min(Convert.ToInt64(new BigIntValue(amountA).Mul(lpTokenInfo.Supply).Div(reserves[0]).Value),
                    Convert.ToInt64(new BigIntValue(amountB).Mul(lpTokenInfo.Supply).Div(reserves[1]).Value));
            Assert(liquidity > 0, "Insufficient liquidity supply.");// Which is impossible due to the TotalSupply is long.MaxValue.
            if (lpTokenInfo.Supply == 0)
            {
                var zeroContractAddress = Context.GetZeroSmartContractAddress();
                State.LPTokenContract.Issue.Send(new IssueInput
                {
                    To = zeroContractAddress,
                    Symbol = lpTokenSymbol,
                    Amount = 1
                });
            }

            State.LPTokenContract.Issue.Send(new IssueInput
            {
                To = account,
                Symbol = lpTokenSymbol,
                Amount = liquidity
            });
         
        
            var pairString = GetTokenPair(tokenA, tokenB);

            Update(balanceA, balanceB, reserves[0], reserves[1], tokenA, tokenB);
            if (feeOn) State.KValueMap[pairAddress] = new BigIntValue(balanceA).Mul(balanceB);

            Context.Fire(new LiquidityAdded
            {
                AmountA = amountA,
                AmountB = amountB,
                LiquidityToken = liquidity,
                Sender = Context.Sender,
                SymbolA = tokenA,
                SymbolB = tokenB,
                Pair = pairAddress,
                To = account,
                Channel = channel
            });
            return liquidity;
        }

        private long[] PerformBurn(Address to, string tokenA, string tokenB, long liquidityRemoveAmount)
        {
            var pairAddress = GetPairAddress(tokenA, tokenB);
            var balanceA = new BigIntValue(State.PoolBalanceMap[pairAddress][tokenA]);
            var balanceB = new BigIntValue(State.PoolBalanceMap[pairAddress][tokenB]);
            var reserves = GetReserves(pairAddress, tokenA, tokenB);
            var feeOn = MintFee(reserves[0], reserves[1], pairAddress, tokenA, tokenB);
            var lpTokenSymbol = GetTokenPairSymbol(tokenA, tokenB);
            var lpTokenInfo = State.LPTokenContract.GetTokenInfo.Call(new Awaken.Contracts.Token.GetTokenInfoInput
            {
                Symbol = lpTokenSymbol
            });
            var liquidityRemoveAmountBigIntValue = new BigIntValue(liquidityRemoveAmount);
            var amountA = Convert.ToInt64(liquidityRemoveAmountBigIntValue.Mul(balanceA).Div(lpTokenInfo.Supply).Value);
            var amountB = Convert.ToInt64(liquidityRemoveAmountBigIntValue.Mul(balanceB).Div(lpTokenInfo.Supply).Value);
            Assert(amountA > 0 && amountB > 0, "Insufficient Liquidity burned");
            DoTransferLPTokens(Context.Sender, Context.Self, lpTokenSymbol, liquidityRemoveAmount);
            State.LPTokenContract.Burn.Send(new BurnInput
            {
                Symbol = lpTokenSymbol,
                Amount = liquidityRemoveAmount
            });
            TransferOut(pairAddress, to, tokenA, amountA);
            TransferOut(pairAddress, to, tokenB, amountB);
            var balanceANew = Convert.ToInt64(balanceA.Sub(amountA).Value);
            var balanceBNew = Convert.ToInt64(balanceB.Sub(amountB).Value);
            var lpTokenBalance = State.LPTokenContract.GetBalance.Call(new Awaken.Contracts.Token.GetBalanceInput
            {
                Owner = Context.Sender,
                Symbol = lpTokenSymbol
            }).Amount;

            Update(balanceANew, balanceBNew, reserves[0], reserves[1], tokenA, tokenB);
            if (feeOn) State.KValueMap[pairAddress] = new BigIntValue(balanceANew).Mul(balanceBNew);
            Context.Fire(new LiquidityRemoved
            {
                AmountA = amountA,
                AmountB = amountB,
                Sender = Context.Sender,
                SymbolA = tokenA,
                SymbolB = tokenB,
                To = to,
                Pair = pairAddress,
                LiquidityToken = liquidityRemoveAmount
            });
            return new[]
            {
                amountA, amountB
            };
        }

        private void Swap(RepeatedField<long> amounts, RepeatedField<string> path, Address lastTo, string channel)
        {
            for (var i = 0; i < path.Count - 1; i++)
            {
                var input = path[i];
                var output = path[i + 1];
                var amountIn = amounts[i];
                var amountOut = amounts[i + 1];
                var to = i < path.Count - 2 ? Context.Self : lastTo;
                var pair = GetPairAddress(path[i], path[i + 1]);
                TransferOut(pair, to, output, amountOut);
                if (i < path.Count - 2)
                {
                    var nextPair = GetPairAddress(output, path[i + 2]);
                    State.PoolBalanceMap[nextPair][output] = State.PoolBalanceMap[nextPair][output].Add(amountOut);
                }

                SwapInternal(input, output, amountIn, amountOut, to, channel);
            }
        }

        private void SwapSupportingFeeOnTransferTokens(RepeatedField<string> path,
            Address lastTo, string channel)
        {
            for (var i = 0; i < path.Count - 1; i++)
            {
                var input = path[i];
                var output = path[i + 1];
                var pair = GetPairAddress(input, output);
                var reserves = GetReserves(pair, input, output);
                var amountInput = State.PoolBalanceMap[pair][input].Sub(reserves[0]);
                var amountOutput = GetAmountOut(amountInput, reserves[0], reserves[1]);
                var to = i < path.Count - 2 ? Context.Self : lastTo;

                TransferOut(pair, to, output, amountOutput);
                if (i < path.Count - 2)
                {
                    var nextPair = GetPairAddress(output, path[i + 2]);
                    State.PoolBalanceMap[nextPair][output] = State.PoolBalanceMap[nextPair][output].Add(amountOutput);
                }

                SwapInternal(input, output, amountInput, amountOutput, to, channel);
            }
        }

        private void SwapInternal(string symbolIn, string symbolOut, long amountIn, long amountOut, Address to,
            string channel)
        {
            var pairAddress = GetPairAddress(symbolIn, symbolOut);
            var reserveSymbolIn = State.TotalReservesMap[pairAddress][symbolIn];
            var reserveSymbolOut = State.TotalReservesMap[pairAddress][symbolOut];
            Assert(amountOut < reserveSymbolOut, "Insufficient reserve of out token");
            Assert(to != pairAddress, "Invalid account address");

            var feeRate = State.FeeRate.Value;
            var totalFee = amountIn.Mul(feeRate).Div(FeeRateMax);

            var balanceIn = State.PoolBalanceMap[pairAddress][symbolIn];
            var balanceOut = State.PoolBalanceMap[pairAddress][symbolOut];
            Assert(amountIn > 0, "Insufficient Input amount");
            var balance0Adjusted = new BigIntValue(balanceIn.Mul(FeeRateMax).Sub(amountIn.Mul(feeRate)));
            var balance1Adjusted = new BigIntValue(balanceOut);

            var reserveSymbolInBigIntValue = new BigIntValue(reserveSymbolIn);
            var reserveSymbolOutBigIntValue = new BigIntValue(reserveSymbolOut);

            Assert(
                balance0Adjusted.Mul(balance1Adjusted) >
                reserveSymbolInBigIntValue.Mul(reserveSymbolOutBigIntValue).Mul(FeeRateMax),
                "Error with K");
            Update(balanceIn, balanceOut, reserveSymbolIn, reserveSymbolOut, symbolIn, symbolOut);
            Context.Fire(new Swap()
            {
                SymbolIn = symbolIn,
                SymbolOut = symbolOut,
                AmountIn = amountIn,
                AmountOut = amountOut,
                Sender = Context.Sender,
                TotalFee = totalFee,
                Pair = pairAddress,
                To = to,
                Channel = channel
            });
        }

        private void Update(long balanceA, long balanceB, long reserveA, long reserveB, string tokenA,
            string tokenB)
        {
            var pairAddress = GetPairAddress(tokenA, tokenB);
            var blockTimestamp = Context.CurrentBlockTime.Seconds;
            var timeElapsed = blockTimestamp - State.LastBlockTimestampMap[pairAddress];
            if (timeElapsed > 0 && reserveA != 0 && reserveB != 0)
            {
                var timeElapsedBigIntValue = new BigIntValue(timeElapsed);
                var reserveABigIntValue = new BigIntValue(reserveA);
                var reserveBBigIntValue = new BigIntValue(reserveB);
                var priceCumulativeA = reserveBBigIntValue.Mul(timeElapsedBigIntValue).Div(reserveABigIntValue);
                var priceCumulativeB = reserveABigIntValue.Mul(timeElapsedBigIntValue).Div(reserveBBigIntValue);
                State.PriceCumulativeLast[pairAddress][tokenA] = State.PriceCumulativeLast[pairAddress][tokenA] == null
                    ? priceCumulativeA
                    : State.PriceCumulativeLast[pairAddress][tokenA].Add(priceCumulativeA);
                State.PriceCumulativeLast[pairAddress][tokenB] = State.PriceCumulativeLast[pairAddress][tokenB] == null
                    ? priceCumulativeB
                    : State.PriceCumulativeLast[pairAddress][tokenB].Add(priceCumulativeB);
            }

            State.TotalReservesMap[pairAddress][tokenA] = balanceA;
            State.TotalReservesMap[pairAddress][tokenB] = balanceB;
            State.LastBlockTimestampMap[pairAddress] = blockTimestamp;
            Context.Fire(new Sync
            {
                ReserveA = balanceA,
                ReserveB = balanceB,
                SymbolA = tokenA,
                SymbolB = tokenB,
                Pair = pairAddress
            });
        }

        private void Skim(string symbolA, string symbolB, Address to)
        {
            var pairAddress = State.PairVirtualAddressMap[symbolA][symbolB];
            var balanceA = GetBalance(symbolA, pairAddress);
            var balanceB = GetBalance(symbolB, pairAddress);
            var reserveSymbolA = State.TotalReservesMap[pairAddress][symbolA];
            var reserveSymbolB = State.TotalReservesMap[pairAddress][symbolB];
            var amountATransfer = balanceA.Sub(reserveSymbolA);
            var amountBTransfer = balanceB.Sub(reserveSymbolB);
            Assert(amountATransfer >= 0 && amountBTransfer >= 0, "Error Skim");
            TransferOut(pairAddress, to, symbolA, amountATransfer);
            TransferOut(pairAddress, to, symbolB, amountBTransfer);
        }

        private void TransferIn(Address pair, Address from, string symbol, long amount)
        {
            State.TokenContract.TransferFrom.Send(
                new TransferFromInput
                {
                    Symbol = symbol,
                    Amount = amount,
                    From = from,
                    Memo = "TransferIn",
                    To = Context.Self
                });
            State.PoolBalanceMap[pair][symbol] = State.PoolBalanceMap[pair][symbol].Add(amount);
        }

        private void TransferOut(Address pair, Address to, string symbol, long amount)
        {
            var poolBalance = State.PoolBalanceMap[pair][symbol];
            Assert(poolBalance > amount, "TransferOut failed");
            State.PoolBalanceMap[pair][symbol] = poolBalance.Sub(amount);
            if (to != Context.Self)
            {
                State.TokenContract.Transfer.Send(
                    new AElf.Contracts.MultiToken.TransferInput()
                    {
                        Symbol = symbol,
                        Amount = amount,
                        Memo = "TransferOut",
                        To = to
                    });
                var balance = GetBalance(symbol, Context.Self);
                if (balance < amount)
                {
                    ReBalance(symbol, amount.Sub(balance));
                }
            }
        }

        //TO DO  withdraw token form vault
        private void ReBalance(string token, long amount)
        {
        }

        private long GetBalance(string symbol, Address address)
        {
            var balance = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Owner = address,
                Symbol = symbol
            });
            return balance.Balance;
        }

        private static BigIntValue Sqrt(BigIntValue n)
        {
            if (n.Value == "0")
                return n;
            var left = new BigIntValue(1);
            var right = n;
            var mid = left.Add(right).Div(2);
            while (!left.Equals(right) && !mid.Equals(left))
            {
                if (mid.Equals(n.Div(mid)))
                    return mid;
                if (mid < n.Div(mid))
                {
                    left = mid;
                    mid = left.Add(right).Div(2);
                }
                else
                {
                    right = mid;
                    mid = left.Add(right).Div(2);
                }
            }

            return left;
        }

        /// <summary>
        /// get equivalent amount of the other token in circumstances of this reserves of pairVirtualAddress
        /// </summary>
        /// <param name="amountA"></param>
        /// <param name="reserveA"></param>
        /// <param name="reserveB"></param>
        /// <returns>equivalent amount of tokenB</returns>
        private long Quote(long amountA, long reserveA, long reserveB)
        {
            Assert(amountA > 0, "Insufficient amount.");
            Assert(reserveA > 0 && reserveB > 0, "Insufficient reserves.");
            var amountB = Convert.ToInt64(new BigIntValue(amountA).Mul(new BigIntValue(reserveB))
                .Div(new BigIntValue(reserveA)).Value);
            return amountB;
        }

        private long GetAmountIn(long amountOut, long reserveIn, long reserveOut)
        {
            Assert(amountOut > 0, "Insufficient Input amount");
            Assert(reserveIn > 0 && reserveOut > 0 && reserveOut > amountOut, "Insufficient reserves");

            var reserveInBigIntValue = new BigIntValue(reserveIn);
            var reserveOutBigIntValue = new BigIntValue(reserveOut);
            var feeRate = new BigIntValue(State.FeeRate.Value);
            var feeRateRest = new BigIntValue(FeeRateMax).Sub(feeRate);
            var numerator = reserveInBigIntValue.Mul(amountOut).Mul(FeeRateMax);
            var denominator = (reserveOutBigIntValue.Sub(amountOut)).Mul(feeRateRest);
            var amountIn = Convert.ToInt64(numerator.Div(denominator).Add(1).Value);
            return amountIn;
        }

        private long GetAmountOut(long amountIn, long reserveIn, long reserveOut)
        {
            Assert(amountIn > 0, "Insufficient Output amount");
            Assert(reserveIn > 0 && reserveOut > 0, "Insufficient reserves");
            var reserveInBigIntValue = new BigIntValue(reserveIn);
            var reserveOutBigIntValue = new BigIntValue(reserveOut);

            var feeRate = new BigIntValue(State.FeeRate.Value);
            var feeRateRest = new BigIntValue(FeeRateMax).Sub(feeRate);

            var amountInWithFee = feeRateRest.Mul(amountIn);
            var numerator = amountInWithFee.Mul(reserveOutBigIntValue);
            var denominator = reserveInBigIntValue.Mul(FeeRateMax).Add(amountInWithFee);
            var amountOut = Convert.ToInt64(numerator.Div(denominator).Value);
            return amountOut;
        }

        private RepeatedField<long> GetAmountsIn(long amountOut, RepeatedField<string> path)
        {
            Assert(path.Count >= 2, "Invalid path");
            var amounts = new RepeatedField<long>() {amountOut};
            for (var i = path.Count - 1; i > 0; i--)
            {
                var symbols = SortSymbols(path[i - 1], path[i]);
                ValidPairSymbol(symbols[0], symbols[1]);
                var reserves = GetReserves(State.PairVirtualAddressMap[symbols[0]][symbols[1]], path[i - 1], path[i]);
                amounts.Insert(0, GetAmountIn(amounts[0], reserves[0], reserves[1]));
            }

            return amounts;
        }

        private RepeatedField<long> GetAmountsOut(long amountIn, RepeatedField<string> path)
        {
            Assert(path.Count >= 2, "Invalid path");
            var amounts = new RepeatedField<long> {amountIn};
            amounts[0] = amountIn;
            for (var i = 0; i < path.Count - 1; i++)
            {
                var symbols = SortSymbols(path[i], path[i + 1]);
                ValidPairSymbol(symbols[0], symbols[1]);
                var reserves = GetReserves(State.PairVirtualAddressMap[symbols[0]][symbols[1]], path[i], path[i + 1]);
                amounts.Add(GetAmountOut(amounts[i], reserves[0], reserves[1]));
            }

            return amounts;
        }

        private bool MintFee(long reserve0, long reserve1, Address pairAddress, string tokenA, string tokenB)
        {
            var feeTo = State.FeeTo.Value;
            var feeOn = feeTo != null;
            var kLast = State.KValueMap[pairAddress];
            if (feeOn)
            {
                if (kLast != null && kLast.Value != "0")
                {
                    var rootK = Sqrt(new BigIntValue(reserve0).Mul(reserve1));
                    var rootKLast = Sqrt(kLast);
                    if (rootK > rootKLast)
                    {
                        var lpTokenSymbol = GetTokenPairSymbol(tokenA, tokenB);
                        var lpTokenInfo = State.LPTokenContract.GetTokenInfo.Call(
                            new Awaken.Contracts.Token.GetTokenInfoInput
                            {
                                Symbol = lpTokenSymbol
                            });
                        var totalSupply = new BigIntValue(lpTokenInfo.Supply);
                        var numerator = new BigIntValue(totalSupply).Mul(rootK.Sub(rootKLast));
                        var denominator = rootK.Mul(4).Add(rootKLast);
                        var liquidity = Convert.ToInt64(numerator.Div(denominator).Value);
                        if (liquidity > 0)
                        {
                            State.LPTokenContract.Issue.Send(new IssueInput
                            {
                                Symbol = lpTokenSymbol,
                                To = feeTo,
                                Amount = liquidity
                            });
                        }
                    }
                }
            }
            else
            {
                State.KValueMap[pairAddress] = new BigIntValue(0);
            }

            return feeOn;
        }

        private void AssertContractInitialized()
        {
            Assert(State.Admin.Value != null, "Contract not initialized.");
        }

        private void AssertSenderIsAdmin()
        {
            AssertContractInitialized();
            Assert(Context.Sender == State.Admin.Value, "No permission.");
        }

        // ReSharper disable once InconsistentNaming
        private void DoTransferLPTokens(Address from, Address to, string symbol, long amount, string memo = "")
        {
            if (from == to) return;
            if (from == Context.Self)
            {
                State.LPTokenContract.Transfer.Send(new TransferInput
                {
                    To = to,
                    Symbol = symbol,
                    Amount = amount,
                    Memo = memo
                });
            }
            else
            {
                State.LPTokenContract.TransferFrom.Send(new Awaken.Contracts.Token.TransferFromInput
                {
                    From = from,
                    To = to,
                    Symbol = symbol,
                    Amount = amount,
                    Memo = memo
                });
            }
        }
    }
}