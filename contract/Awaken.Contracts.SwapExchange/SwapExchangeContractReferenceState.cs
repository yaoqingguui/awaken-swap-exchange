using Awaken.Contracts.Swap;
using Awaken.Contracts.Token;

namespace Awaken.Contracts.SwapExchangeContract
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class SwapExchangeContractState
    {
        // state definitions go here.
        internal AwakenSwapContractContainer.AwakenSwapContractReferenceState SwapContract { get; set; }

        internal TokenContractContainer.TokenContractReferenceState LpTokenContract { get; set; }

        internal AElf.Contracts.MultiToken.TokenContractContainer.TokenContractReferenceState CommonTokenContract
        {
            get;
            set;
        }
    }
}