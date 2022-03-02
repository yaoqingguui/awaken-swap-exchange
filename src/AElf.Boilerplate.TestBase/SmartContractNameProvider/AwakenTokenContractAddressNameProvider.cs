using AElf.Kernel.Infrastructure;
using AElf.Types;

namespace AElf.Boilerplate.TestBase.SmartContractNameProvider
{
    public class AwakenTokenContractAddressNameProvider
    {
        public static readonly Hash Name = HashHelper.ComputeFrom("Awaken.ContractNames.Token");
        
        public static readonly string StringName = Name.ToStorageKey();
        public Hash ContractName => Name;
        public string ContractStringName => StringName;
    }
}