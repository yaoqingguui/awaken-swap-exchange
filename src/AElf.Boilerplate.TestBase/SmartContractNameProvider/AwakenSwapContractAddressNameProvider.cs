using AElf.Kernel.Infrastructure;
using AElf.Types;

namespace AElf.Boilerplate.TestBase.SmartContractNameProvider
{
    public class AwakenSwapContractAddressNameProvider
    {
        public static readonly Hash Name = HashHelper.ComputeFrom("Awaken.Contracts.Swap");

        public static readonly string StringName = Name.ToStorageKey();
        public Hash ContractName => Name;
        public string ContractStringName => StringName; 
    }
}