
namespace AzureAppGatewayOrchestrator.Tests;

public sealed class IntegrationTestingFact : FactAttribute
{
    public string HttpsListenerName { get; private set; }
    public string TenantId { get; private set; }
    public string ApplicationId { get; private set; }
    public string ClientSecret { get; private set; }
    public string ResourceId { get; private set; }

    public IntegrationTestingFact()
    {
        HttpsListenerName = Environment.GetEnvironmentVariable("AZURE_APP_GATEWAY_HTTPS_LISTENER_NAME") ?? string.Empty;
        TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? string.Empty;
        ApplicationId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? string.Empty;
        ClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? string.Empty;
        ResourceId = Environment.GetEnvironmentVariable("AZURE_APP_GATEWAY_RESOURCE_ID") ?? string.Empty;

        if (string.IsNullOrEmpty(HttpsListenerName) || string.IsNullOrEmpty(TenantId) || string.IsNullOrEmpty(ApplicationId) || string.IsNullOrEmpty(ClientSecret) || string.IsNullOrEmpty(ResourceId))
        {
            Skip = "Integration testing environment variables are not set - Skipping test. Please run `make setup` to set the environment variables.";
        }
    }
}

