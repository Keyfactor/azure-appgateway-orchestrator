using System.Runtime.CompilerServices;
using Azure.Core;
using Keyfactor.Extensions.Orchestrator.AzureAppGateway.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.AzureAppGateway.Jobs
{
    public abstract class AzureAppGatewayJob<T> : IOrchestratorJobExtension
    {
        public string ExtensionName => "AzureAppGW";

        protected AzureAppGatewayClient GatewayClient { get; private set; }

        protected void Initialize(CertificateStore details)
        {
            ILogger logger = LogHandler.GetReflectedClassLogger(this);
            logger.LogDebug($"Certificate Store Configuration: {JsonConvert.SerializeObject(details)}");
            logger.LogDebug("Initializing AzureAppGatewayClient");
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
            ILogger logger = LogHandler.GetReflectedClassLogger(this);
            logger.LogDebug($"Discovery Job Configuration: {JsonConvert.SerializeObject(config)}");
            logger.LogDebug("Initializing AzureAppGatewayClient");
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