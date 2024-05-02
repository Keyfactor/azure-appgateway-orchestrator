// Copyright 2024 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace AzureAppGatewayOrchestrator.Tests;

public sealed class IntegrationTestingFact : FactAttribute
{
    public string HttpsListenerName { get; private set; }
    public string TenantId { get; private set; }
    public string ApplicationId { get; private set; }
    public string ClientSecret { get; private set; }
    public string ClientCertificatePath { get; private set; }
    public string ResourceId { get; private set; }

    public IntegrationTestingFact()
    {
        HttpsListenerName = Environment.GetEnvironmentVariable("AZURE_APP_GATEWAY_HTTPS_LISTENER_NAME") ?? string.Empty;
        TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? string.Empty;
        ApplicationId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? string.Empty;
        ClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? string.Empty;
        ClientCertificatePath = Environment.GetEnvironmentVariable("AZURE_PATH_TO_CLIENT_CERTIFICATE") ?? string.Empty;
        ResourceId = Environment.GetEnvironmentVariable("AZURE_APP_GATEWAY_RESOURCE_ID") ?? string.Empty;

        if (string.IsNullOrEmpty(HttpsListenerName) || string.IsNullOrEmpty(TenantId) || string.IsNullOrEmpty(ApplicationId) || string.IsNullOrEmpty(ClientSecret) || string.IsNullOrEmpty(ResourceId) || string.IsNullOrEmpty(ClientCertificatePath))
        {
            Skip = "Integration testing environment variables are not set - Skipping test. Please run `make setup` to set the environment variables.";
        }
    }
}

public sealed class IntegrationTestingTheory : TheoryAttribute
{
    public string HttpsListenerName { get; private set; }
    public string TenantId { get; private set; }
    public string ApplicationId { get; private set; }
    public string ClientSecret { get; private set; }
    public string ClientCertificatePath { get; private set; }
    public string ResourceId { get; private set; }

    public IntegrationTestingTheory()
    {
        HttpsListenerName = Environment.GetEnvironmentVariable("AZURE_APP_GATEWAY_HTTPS_LISTENER_NAME") ?? string.Empty;
        TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? string.Empty;
        ApplicationId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? string.Empty;
        ClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? string.Empty;
        ClientCertificatePath = Environment.GetEnvironmentVariable("AZURE_PATH_TO_CLIENT_CERTIFICATE") ?? string.Empty;
        ResourceId = Environment.GetEnvironmentVariable("AZURE_APP_GATEWAY_RESOURCE_ID") ?? string.Empty;

        if (string.IsNullOrEmpty(HttpsListenerName) || string.IsNullOrEmpty(TenantId) || string.IsNullOrEmpty(ApplicationId) || string.IsNullOrEmpty(ClientSecret) || string.IsNullOrEmpty(ResourceId) || string.IsNullOrEmpty(ClientCertificatePath))
        {
            Skip = "Integration testing environment variables are not set - Skipping test. Please run `make setup` to set the environment variables.";
        }
    }
}
