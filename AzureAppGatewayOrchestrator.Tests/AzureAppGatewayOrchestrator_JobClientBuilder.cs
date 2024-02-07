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

using AzureAppGatewayOrchestrator.Tests;
using AzureApplicationGatewayOrchestratorExtension;
using AzureApplicationGatewayOrchestratorExtension.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

public class AzureAppGatewayOrchestrator_JobClientBuilder
{
    ILogger _logger { get; set;}

    public AzureAppGatewayOrchestrator_JobClientBuilder()
    {
        ConfigureLogging();

        _logger = LogHandler.GetClassLogger<AzureAppGatewayOrchestrator_AzureAppGw>();
    }

    [Fact]
    public void AppGatewayJobClientBuilder_ValidCertificateStoreConfig_BuildValidClient()
    {
        // Verify that the AppGatewayJobClientBuilder uses the certificate store configuration
        // provided by Keyfactor Command/the Universal Orchestrator correctly as required
        // by the IAzureAppGatewayClientBuilder interface.

        // Arrange
        AppGatewayJobClientBuilder<FakeClient.FakeBuilder> jobClientBuilderWithFakeBuilder = new();

        // Set up the certificate store with names that correspond to how we expect them to be interpreted by
        // the builder
        CertificateStore fakeCertificateStoreDetails = new()
        {
            ClientMachine = "fake-tenant-id",
            StorePath = "fake-azure-resource-id",
            Properties = "{\"ServerUsername\":\"fake-azure-application-id\",\"ServerPassword\":\"fake-azure-client-secret\",\"AzureCloud\":\"fake-azure-cloud\"}"
        };

        // Act
        IAzureAppGatewayClient fakeAppGatewayClient = jobClientBuilderWithFakeBuilder
            .WithCertificateStoreDetails(fakeCertificateStoreDetails)
            .Build();

        // Assert

        // IAzureAppGatewayClient doesn't require any of the properties set by the builder to be exposed
        // since the production Build() method creates an Azure Resource Manager client.
        // But, our builder is fake and exposes the properties we need to test (via the FakeBuilder class).
        Assert.Equal("fake-tenant-id", jobClientBuilderWithFakeBuilder._builder._tenantId);
        Assert.Equal("fake-azure-resource-id", jobClientBuilderWithFakeBuilder._builder._resourceId);
        Assert.Equal("fake-azure-application-id", jobClientBuilderWithFakeBuilder._builder._applicationId);
        Assert.Equal("fake-azure-client-secret", jobClientBuilderWithFakeBuilder._builder._clientSecret);
        Assert.Equal("fake-azure-cloud", jobClientBuilderWithFakeBuilder._builder._azureCloudEndpoint);

        _logger.LogInformation("AppGatewayJobClientBuilder_ValidCertificateStoreConfig_BuildValidClient - Success");
    }
    
    static void ConfigureLogging()
    {
        var config = new NLog.Config.LoggingConfiguration();

        // Targets where to log to: File and Console
        var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
        logconsole.Layout = @"${date:format=HH\:mm\:ss} ${logger} [${level}] - ${message}";

        // Rules for mapping loggers to targets            
        config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, logconsole);

        // Apply config           
        NLog.LogManager.Configuration = config;

        LogHandler.Factory = LoggerFactory.Create(builder =>
                {
                builder.AddNLog();
                });
    }
}
