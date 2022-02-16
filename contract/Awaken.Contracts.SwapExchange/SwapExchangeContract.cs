using System;
using System.Linq;
using Google.Protobuf.WellKnownTypes;

namespace Awaken.Contracts.SwapExchangeContract
{
    /// <summary>
    /// The C# implementation of the contract defined in swap_exchange_contract.proto that is located in the "protobuf"
    /// folder.
    /// Notice that it inherits from the protobuf generated code. 
    /// </summary>
    public partial class SwapExchangeContract : SwapExchangeContractContainer.SwapExchangeContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(State.Owner.Value == null, "Contract already Initialized.");
            if (input.Onwer!=null)
            {
                State.Owner.Value = input.Onwer;
            }
            else
            {
                State.Owner.Value = Context.Sender;
            }

            State.To.Value = input.To;
            State.TargetToken.Value = input.TargetToken;
            State.SwapContract.Value = input.SwapContract;
            State.LpTokenContract.Value = input.LpTokenContract;
            return new Empty();
        }
        
        
        private void OnlyOwner()
        {
           Assert(State.Owner.Value!=null,"Contract not initialized.");
           Assert(Context.Sender==State.Owner.Value,"Not permission.");
        }

        private string ExtractTokenPairFromSymbol(string symbol)
        {
            Assert(string.IsNullOrEmpty(symbol),"Symbol blank.");
            // ReSharper disable once PossibleNullReferenceException
            return symbol.Substring(symbol.IndexOf("ALP", StringComparison.Ordinal)).Trim();
        }

        private string[] ExtractTokensFromTokenPair(string tokenPair)
        {
            Assert(tokenPair.Contains("-") && tokenPair.Count(c => c == '-') == 1, $"Invalid TokenPair {tokenPair}.");
            return SortSymbols(tokenPair.Split('-'));
        }
        
        private string[] SortSymbols(params string[] symbols)
        {
            Assert(symbols.Length == 2, "Invalid symbols for sorting.");
            return symbols.OrderBy(s => s).ToArray();
        }
        
        
        private void OnlySelf()
        {   
            Assert(Context.Self==Context.Sender,"No permission.");
        }
    }
}