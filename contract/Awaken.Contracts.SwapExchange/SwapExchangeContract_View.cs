using AElf.Types;
using Google.Protobuf.WellKnownTypes;

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
         * Receivor
         */
        public override Address Receivor(Empty input)
        {   
            return State.Receivor.Value;
        }
        
        /**
         * TargetToken
         */
        public override StringValue TargetToken(Empty input)
        {
            return new StringValue
            {
                Value = State.TargetToken.Value
            };
        }
    }
}