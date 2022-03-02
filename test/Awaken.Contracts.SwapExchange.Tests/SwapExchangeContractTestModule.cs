using System.Collections.Generic;
using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.ContractTestBase;
using AElf.Kernel.SmartContract.Application;
using Awaken.Contracts.Swap;
using Awaken.Contracts.SwapExchange.ContractInitializationProviders;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using TokenContract = Awaken.Contracts.Token.TokenContract;

namespace Awaken.Contracts.SwapExchange
{
    [DependsOn(typeof(MainChainDAppContractTestModule))]
    public class SwapExchangeContractTestModule : MainChainDAppContractTestModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton<IContractInitializationProvider, SwapExchangeContractInitializationProvider>();
            context.Services.AddSingleton<IContractInitializationProvider, AwakenTokenInitializationProvider>();
            context.Services.AddSingleton<IContractInitializationProvider, AwakenSwapContractInitializationProvider>();
        }

        public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
        {
            var contractCodeProvider = context.ServiceProvider.GetService<IContractCodeProvider>();
            var contractDllLocation = typeof(Contracts.SwapExchangeContract.SwapExchangeContract).Assembly.Location;
            var contractCodes = new Dictionary<string, byte[]>(contractCodeProvider.Codes)
            {
                {
                    new SwapExchangeContractInitializationProvider().ContractCodeName,
                    File.ReadAllBytes(contractDllLocation)
                },
                {
                    new AwakenTokenInitializationProvider().ContractCodeName,
                    File.ReadAllBytes(typeof(TokenContract).Assembly.Location)
                },
                {
                    new AwakenSwapContractInitializationProvider().ContractCodeName,
                    File.ReadAllBytes(typeof(AwakenSwapContract).Assembly.Location)
                }
            };
            contractCodeProvider.Codes = contractCodes;
        }
    }
}