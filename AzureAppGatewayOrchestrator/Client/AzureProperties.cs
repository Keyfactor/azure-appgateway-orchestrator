using Azure.Core;

namespace Keyfactor.Extensions.Orchestrator.AzureAppGateway.Client
{
    public class AzureProperties
    {
        public string TenantId { get; set; }
        public string ApplicationId { get; set; }
        public string ClientSecret { get; set; }
        public ResourceIdentifier GatewayResourceId { get; set; }
    }
}