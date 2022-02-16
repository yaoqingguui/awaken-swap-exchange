using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Awaken.Contracts.SwapExchangeContract
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class SwapExchangeContractState : ContractState
    {
        
        public SingletonState<Address> Owner { get; set; }
        
        public StringState TargetToken { get; set; }
        
        public SingletonState<Address> To { get; set; }
        
        // Common token symbol and cumulative amount after remove liquity.
        public SingletonState<TokenList> CumulativeTokenList { get; set; }
    } 
}