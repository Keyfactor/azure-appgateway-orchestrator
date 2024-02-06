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

using System.Security.Cryptography.X509Certificates;
using Azure.ResourceManager.Network.Models;
using AzureApplicationGatewayOrchestratorExtension.Client;
using AzureApplicationGatewayOrchestratorExtension.ListenerBindingJobs;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace AzureAppGatewayOrchestrator.Tests;

public class AzureAppGatewayOrchestrator_AppGwBin
{
    ILogger _logger { get; set;}

    public AzureAppGatewayOrchestrator_AppGwBin()
    {
        ConfigureLogging();

        _logger = LogHandler.GetClassLogger<AzureAppGatewayOrchestrator_AppGwBin>();
    }

    [IntegrationTestingFact]
    public void AppGwBin_Inventory_IntegrationTest_ReturnSuccess()
    {
        // Arrange
        IntegrationTestingFact env = new();

        // Set up the inventory job configuration
        var config = new InventoryJobConfiguration
        {
            CertificateStoreDetails = new CertificateStore
            {
                ClientMachine = env.TenantId,
                              StorePath = env.ResourceId,
                              Properties = $"{{\"ServerUsername\":\"{env.ApplicationId}\",\"ServerPassword\":\"{env.ClientSecret}\",\"AzureCloud\":\"\"}}"
            }
        };

        var inventory = new Inventory();

        // Act
        JobResult result = inventory.ProcessJob(config, (inventoryItems) =>
        {
            // Assert
            Assert.NotNull(inventoryItems);
            Assert.NotEmpty(inventoryItems);

            _logger.LogInformation("AppGwBin_Inventory_IntegrationTest_ReturnSuccess - Success");
            return true;
        });

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
    }

    [Fact]
    public void AppGwBin_Inventory_ProcessJob_ValidClientOneBoundHttpsListener_ReturnSuccess()
    {
        // Arrange
        IAzureAppGatewayClient client = new FakeClient
        {
            CertificatesAvailableOnFakeAppGateway = new Dictionary<string, ApplicationGatewaySslCertificate>
            {
                // Internally, FakeClient tracks certificates bound to listeners with the Password property
                { "test", new ApplicationGatewaySslCertificate { Name = "test-certificate", Password = "fake-http-listener" } }
            }
        };

        // Set up the inventory job with the fake client
        var inventory = new Inventory
        {
            Client = client
        };

        // Set up the inventory job configuration
        var config = new InventoryJobConfiguration
        {
            CertificateStoreDetails = new CertificateStore
            {
                ClientMachine = "test",
                              StorePath = "test",
                              Properties = "{\"ServerUsername\":\"test\",\"ServerPassword\":\"test\",\"AzureCloud\":\"test\"}"
            },
                JobHistoryId = 1
        };

        // Act
        JobResult result = inventory.ProcessJob(config, (inventoryItems) =>
                {
                // Assert
                Assert.Equal(1, inventoryItems.Count());
                Assert.Equal("fake-http-listener", inventoryItems.First().Alias);

                _logger.LogInformation("AppGwBin_Inventory_ProcessJob_ValidClientOneBoundHttpsListener_ReturnSuccess - Success");
                return true;
                });

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
    }

    [Fact]
    public void AppGwBin_Discovery_ProcessJob_ValidClient_ReturnSuccess()
    {
        // Arrange
        IAzureAppGatewayClient client = new FakeClient
        {
            AppGatewaysAvailableOnFakeTenant = new List<string> { "test" }
        };

        // Set up the discovery job with the fake client
        var discovery = new Discovery
        {
            Client = client
        };

        // Set up the discovery job configuration
        var config = new DiscoveryJobConfiguration
        {
            ClientMachine = "fake-tenant-id",
            ServerUsername = "fake-application-id",
            ServerPassword = "fake-client-secret",
            JobProperties = new Dictionary<string, object>
            {
                { "dirs", "fake-tenant-id" }
            }
        };

        // Act
        JobResult result = discovery.ProcessJob(config, (discoveredAppGateways) =>
        {
            // Assert
            Assert.Equal(1, discoveredAppGateways.Count());
            Assert.Equal("test", discoveredAppGateways.First());

            _logger.LogInformation("Discovery_ProcessJob_ValidClient_ReturnSuccess - Success");
            return true;
        });

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);

        _logger.LogInformation("Discovery_ProcessJob_ValidClient_ReturnSuccess - Success");
    }

    [Fact]
    public void AppGwBin_Discovery_ProcessJob_InvalidClient_ReturnFailure()
    {
        // Arrange
        IAzureAppGatewayClient client = new FakeClient();

        // Set up the discovery job with the fake client
        var discovery = new Discovery
        {
            Client = client
        };

        // Set up the discovery job configuration
        var config = new DiscoveryJobConfiguration
        {
            ClientMachine = "fake-tenant-id",
            ServerUsername = "fake-application-id",
            ServerPassword = "fake-client-secret",
            JobProperties = new Dictionary<string, object>
            {
                { "dirs", "fake-tenant-id" }
            }
        };

        bool callbackCalled = false;

        // Act
        JobResult result = discovery.ProcessJob(config, (discoveredAppGateways) =>
        {
            callbackCalled = true;

            // Assert
            Assert.True(false, "Callback should not be called");
            return true;
        });

        // Assert
        Assert.False(callbackCalled);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);

        _logger.LogInformation("Discovery_ProcessJob_InvalidClient_ReturnFailure - Success");
    }

    [Fact]
    public void AppGwBin_ManagementAdd_ProcessJob_ListenerAlreadyBoundToCertificate_ReturnSuccess()
    {
        // Arrange
        FakeClient client = new FakeClient
        {
            CertificatesAvailableOnFakeAppGateway = new Dictionary<string, ApplicationGatewaySslCertificate>
            {
                // Internally, FakeClient tracks certificates bound to listeners with the Password property
                { "certificate-from-unknown-source", new ApplicationGatewaySslCertificate { Name = "certificate-from-unknown-source", Password = "fake-https-listener" } }
            }
        };

        // Set up the management job with the fake client
        var management = new Management
        {
            Client = client
        };

        // Set up the management job configuration
        var config = new ManagementJobConfiguration
        {
            OperationType = CertStoreOperationType.Add,
            JobCertificate = new ManagementJobCertificate
            {
                Alias = "fake-https-listener",
                Contents = "test-certificate-data",
                PrivateKeyPassword = "test-password"
            },
            JobHistoryId = 1
        };

        // Act
        JobResult result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
        Assert.Equal(1, result.JobHistoryId);
        Assert.NotNull(client.CertificatesAvailableOnFakeAppGateway);
        if (client.CertificatesAvailableOnFakeAppGateway != null)
        {
            Assert.True(client.CertificatesAvailableOnFakeAppGateway.ContainsKey("fake-https-listener"));
            Assert.True(client.CertificatesAvailableOnFakeAppGateway["fake-https-listener"].Password == "fake-https-listener");

            // The original certificate should still be present, but not bound to the listener
            Assert.True(client.CertificatesAvailableOnFakeAppGateway.ContainsKey("certificate-from-unknown-source"));
            Assert.False(client.CertificatesAvailableOnFakeAppGateway["certificate-from-unknown-source"].Password == "fake-https-listener");
            Assert.True(client.CertificatesAvailableOnFakeAppGateway.Count == 2);
        }

        _logger.LogInformation("AppGwBin_ManagementAdd_ProcessJob_ValidClient_ReturnSuccess - Success");
    }

    [Fact]
    public void AppGwBin_ManagementAdd_ProcessJob_ListenerCertificateAddedByExtension_ReturnSuccess()
    {
        // Arrange
        FakeClient client = new FakeClient
        {
            CertificatesAvailableOnFakeAppGateway = new Dictionary<string, ApplicationGatewaySslCertificate>
            {
                // Internally, FakeClient tracks certificates bound to listeners with the Password property
                { "fake-https-listener", new ApplicationGatewaySslCertificate { Name = "fake-https-listener", Password = "fake-https-listener" } }
            }
        };

        // Set up the management job with the fake client
        var management = new Management
        {
            Client = client
        };

        // Set up the management job configuration
        var config = new ManagementJobConfiguration
        {
            OperationType = CertStoreOperationType.Add,
            Overwrite = true,
            JobCertificate = new ManagementJobCertificate
            {
                Alias = "fake-https-listener",
                Contents = "test-certificate-data",
                PrivateKeyPassword = "test-password"
            },
            JobHistoryId = 1
        };

        // Act
        JobResult result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
        Assert.Equal(1, result.JobHistoryId);
        Assert.NotNull(client.CertificatesAvailableOnFakeAppGateway);
        if (client.CertificatesAvailableOnFakeAppGateway != null)
        {
            Assert.True(client.CertificatesAvailableOnFakeAppGateway.ContainsKey("fake-https-listener"));

            // Internally, FakeClient represents HTTPS listeners bound to certificates with the Password field.
            Assert.True(client.CertificatesAvailableOnFakeAppGateway["fake-https-listener"].Password == "fake-https-listener");
            Assert.True(client.CertificatesAvailableOnFakeAppGateway.Count == 1);
        }

        _logger.LogInformation("AppGwBin_ManagementReplace_ProcessJob_ValidClient_ReturnSuccess - Success");
    }

    [IntegrationTestingFact]
    public void AppGwBin_Management_IntegrationTest_AddAndBindCertificate_ReturnSuccess()
    {
        // Arrange
        IntegrationTestingFact env = new();

        // Items necessary for cleanup
        IAzureAppGatewayClient client = new GatewayClient.Builder()
            .WithTenantId(env.TenantId)
            .WithApplicationId(env.ApplicationId)
            .WithClientSecret(env.ClientSecret)
            .WithResourceId(env.ResourceId)
            .Build();

        IDictionary<string, string> currentlyBoundCertificates = client.GetBoundHttpsListenerCertificates();
        string currentlyBoundCertificate = currentlyBoundCertificates[env.HttpsListenerName];
        _logger.LogTrace($"Certificate called {currentlyBoundCertificate} is currently bound to HTTPS listener {env.HttpsListenerName}");

        string testHostname = "azureappgatewayorchestratorUnitTest.com";
        string certName = "GatewayTest" + Guid.NewGuid().ToString()[..6];
        string password = "password";

        X509Certificate2 ssCert = AzureAppGatewayOrchestrator_Client.GetSelfSignedCert(testHostname);

        string b64PfxSslCert = Convert.ToBase64String(ssCert.Export(X509ContentType.Pfx, password));

        // Set up the management job configuration
        var config = new ManagementJobConfiguration
        {
            OperationType = CertStoreOperationType.Add,
            CertificateStoreDetails = new CertificateStore
            {
                ClientMachine = env.TenantId,
                              StorePath = env.ResourceId,
                              Properties = $"{{\"ServerUsername\":\"{env.ApplicationId}\",\"ServerPassword\":\"{env.ClientSecret}\",\"AzureCloud\":\"\"}}"
            },
            JobCertificate = new ManagementJobCertificate
            {
                Alias = env.HttpsListenerName,
                Contents = b64PfxSslCert,
                PrivateKeyPassword = password
            },
        };

        var management = new Management();

        // Act
        // This will process a Management Add job
        JobResult result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);

        _logger.LogInformation("AzureAppGw_Management_IntegrationTest_ReturnSuccess - Success");
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
