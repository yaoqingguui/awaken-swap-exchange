using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProvider;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;

namespace Awaken.Contracts.SwapExchange.ContractInitializationProviders
{
    public class AwakenSwapContractInitializationProvider : IContractInitializationProvider
    {
        public List<ContractInitializationMethodCall> GetInitializeMethodList(byte[] contractCode)
        {
            return new List<ContractInitializationMethodCall>();
        }

        public Hash SystemSmartContractName { get; } = AwakenSwapContractAddressNameProvider.Name;
        public string ContractCodeName { get; } = "Awaken.Contracts.Swap";
    }
}