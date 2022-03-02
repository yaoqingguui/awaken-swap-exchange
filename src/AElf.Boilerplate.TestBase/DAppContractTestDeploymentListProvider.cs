using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProvider;
using AElf.ContractTestBase;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;

namespace AElf.Boilerplate.TestBase
{
    public class SideChainDAppContractTestDeploymentListProvider : SideChainContractDeploymentListProvider, IContractDeploymentListProvider
    {
        public List<Hash> GetDeployContractNameList()
        {
            var list = base.GetDeployContractNameList();
            list.Add(DAppSmartContractAddressNameProvider.Name);
            return list;
        }
    }
    
    public class MainChainDAppContractTestDeploymentListProvider : MainChainContractDeploymentListProvider, IContractDeploymentListProvider
    {
        public List<Hash> GetDeployContractNameList()
        {
            var list = base.GetDeployContractNameList();
            list.Add(DAppSmartContractAddressNameProvider.Name);
            list.Add(AwakenSwapContractAddressNameProvider.Name);
            list.Add(AwakenTokenContractAddressNameProvider.Name);
            return list;
        }
    }
}