using Azure.Core;
using Keyfactor.Extensions.Orchestrator.AzureAppGateway.Client;
using Keyfactor.Orchestrators.Extensions;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.AzureAppGateway.Jobs
{
    public abstract class AzureAppGatewayJob<T> : IOrchestratorJobExtension
    {
        public string ExtensionName => "AzureAppGW";
        
        protected AzureAppGatewayClient GatewayClient { get; private set; }

        protected void Initialize(CertificateStore details)
        {
            dynamic properties = JsonConvert.DeserializeObject(details.Properties);
            
            AzureProperties azureProperties = new AzureProperties
            {
                TenantId = details.ClientMachine,
                ApplicationId = properties?.ServerUsername,
                ClientSecret = properties?.ServerPassword
            };
            
            GatewayClient = new AzureAppGatewayClient(azureProperties)
            {
                AppGatewayResourceId = new ResourceIdentifier(details.StorePath)
            };
        }

        protected void Initialize(DiscoveryJobConfiguration config)
        {
            AzureProperties azureProperties = new AzureProperties
            {
                TenantId = config.ClientMachine,
                ApplicationId = config.ServerUsername,
                ClientSecret = config.ServerPassword
            };
            
            GatewayClient = new AzureAppGatewayClient(azureProperties);
        }
    }
}