using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProvider;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;

namespace Awaken.Contracts.SwapExchange.ContractInitializationProviders
{
    public class AwakenTokenInitializationProvider : IContractInitializationProvider
    {
        public List<ContractInitializationMethodCall> GetInitializeMethodList(byte[] contractCode)
        {
            return new List<ContractInitializationMethodCall>();
        }

        public Hash SystemSmartContractName { get; } = AwakenTokenContractAddressNameProvider.Name;
        public string ContractCodeName { get; } = "Awaken.Contracts.Token";
    }
}