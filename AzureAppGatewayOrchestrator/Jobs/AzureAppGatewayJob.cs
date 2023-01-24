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
                TenantId = properties?.TenantID,
                ApplicationId = properties?.ServerUsername,
                ClientSecret = properties?.ServerPassword,
                GatewayResourceId = new ResourceIdentifier(details.StorePath)
            };
            
            GatewayClient = new AzureAppGatewayClient(azureProperties);
        }

        protected void Initialize(DiscoveryJobConfiguration config)
        {
            AzureProperties azureProperties = new AzureProperties();
            
            GatewayClient = new AzureAppGatewayClient(azureProperties);
        }
    }
}