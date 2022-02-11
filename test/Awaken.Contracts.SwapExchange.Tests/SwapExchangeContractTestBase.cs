using AElf.Boilerplate.TestBase;
using AElf.Cryptography.ECDSA;
using Awaken.Contracts.SwapExchangeContract;

namespace Awaken.Contracts.SwapExchange
{
    public class SwapExchangeContractTestBase : DAppContractTestBase<SwapExchangeContractTestModule>
    {
        // You can get address of any contract via GetAddress method, for example:
        // internal Address DAppContractAddress => GetAddress(DAppSmartContractAddressNameProvider.StringName);

        internal SwapExchangeContractContainer.SwapExchangeContractStub GetSwapExchangeContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<SwapExchangeContractContainer.SwapExchangeContractStub>(DAppContractAddress, senderKeyPair);
        }
    }
}