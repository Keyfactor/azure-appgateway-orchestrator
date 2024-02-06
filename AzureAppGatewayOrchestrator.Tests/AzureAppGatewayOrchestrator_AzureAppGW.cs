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
using AzureApplicationGatewayOrchestratorExtension;
using AzureApplicationGatewayOrchestratorExtension.AppGatewayCertificateJobs;
using AzureApplicationGatewayOrchestratorExtension.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace AzureAppGatewayOrchestrator.Tests;

public class AzureAppGatewayOrchestrator_AzureAppGw
{
    ILogger _logger { get; set;}

    public AzureAppGatewayOrchestrator_AzureAppGw()
    {
        ConfigureLogging();

        _logger = LogHandler.GetClassLogger<AzureAppGatewayOrchestrator_AzureAppGw>();
    }

    [IntegrationTestingFact]
    public void AzureAppGw_Inventory_IntegrationTest_ReturnSuccess()
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

            _logger.LogInformation("AzureAppGw_Inventory_IntegrationTest_ReturnSuccess - Success");
            return true;
        });

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
    }

    [Fact]
    public void AzureAppGw_Inventory_ProcessJob_ValidClient_ReturnSuccess()
    {
        // Arrange
        IAzureAppGatewayClient client = new FakeClient
        {
            CertificatesAvailableOnFakeAppGateway = new Dictionary<string, ApplicationGatewaySslCertificate>
            {
                { "test", new ApplicationGatewaySslCertificate { Name = "test" } }
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
                Assert.Equal("test", inventoryItems.First().Alias);

                _logger.LogInformation("AzureAppGw_Inventory_ProcessJob_ValidClient_ReturnSuccess - Success");
                return true;
                });

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
    }

    [Fact]
    public void AzureAppGw_Inventory_ProcessJob_InvalidClient_ReturnFailure()
    {
        // Arrange
        IAzureAppGatewayClient client = new FakeClient();

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

        bool callbackCalled = false;

        // Act
        JobResult result = inventory.ProcessJob(config, (inventoryItems) =>
        {
            callbackCalled = true;

            // Assert
            Assert.True(false, "Callback should not be called");
            return true;
        });

        // Assert
        Assert.False(callbackCalled);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);

        _logger.LogInformation("AzureAppGw_Inventory_ProcessJob_InvalidClient_ReturnFailure - Success");
    }

    [IntegrationTestingFact]
    public void AzureAppGw_Discovery_IntegrationTest_ReturnSuccess()
    {
        // Arrange
        IntegrationTestingFact env = new();

        // Set up the discovery job configuration
        var config = new DiscoveryJobConfiguration
        {
            ClientMachine = env.TenantId,
            ServerUsername = env.ApplicationId,
            ServerPassword = env.ClientSecret,
            JobProperties = new Dictionary<string, object>
            {
                { "dirs", env.TenantId }
            }
        };

        var discovery = new Discovery();

        // Act
        JobResult result = discovery.ProcessJob(config, (discoveredAppGateways) =>
        {
            // Assert
            Assert.NotNull(discoveredAppGateways);
            Assert.NotEmpty(discoveredAppGateways);

            _logger.LogInformation("AzureAppGw_Discovery_IntegrationTest_ReturnSuccess - Success");
            return true;
        });

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
    }

    [Fact]
    public void AzureAppGw_Discovery_ProcessJob_ValidClient_ReturnSuccess()
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

        _logger.LogInformation("AzureAppGw_Discovery_ProcessJob_ValidClient_ReturnSuccess - Success");
    }

    [Fact]
    public void AzureAppGw_Discovery_ProcessJob_InvalidClient_ReturnFailure()
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

        _logger.LogInformation("AzureAppGw_Discovery_ProcessJob_InvalidClient_ReturnFailure - Success");
    }

    [Fact]
    public void AzureAppGw_ManagementAdd_ProcessJob_ValidClient_ReturnSuccess()
    {
        // Arrange
        FakeClient client = new FakeClient();

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
                Alias = "test",
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
            Assert.True(client.CertificatesAvailableOnFakeAppGateway.ContainsKey("test"));
        }

        _logger.LogInformation("AzureAppGw_ManagementAdd_ProcessJob_ValidClient_ReturnSuccess - Success");
    }

    [Theory]
    [InlineData("test", "")]
    [InlineData("", "test-password")]
    [InlineData("", "")]
    public void AzureAppGw_ManagementAdd_ProcessJob_InvalidJobConfig_ReturnFailure(string alias, string pkPassword)
    {
        // Arrange
        FakeClient client = new FakeClient();

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
                Alias = alias,
                Contents = "test-certificate-data",
                PrivateKeyPassword = pkPassword
            },
            JobHistoryId = 1
        };

        // Act
        JobResult result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal(1, result.JobHistoryId);

        _logger.LogInformation("AzureAppGw_ManagementAdd_ProcessJob_InvalidJobConfig_ReturnFailure - Success");
    }

    [Fact]
    public void AzureAppGw_ManagementRemove_ProcessJob_ValidClient_ReturnSuccess()
    {
        // Arrange
        FakeClient client = new FakeClient
        {
            CertificatesAvailableOnFakeAppGateway = new Dictionary<string, ApplicationGatewaySslCertificate>
            {
                { "test", new ApplicationGatewaySslCertificate { Name = "test" } }
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
            OperationType = CertStoreOperationType.Remove,
            JobCertificate = new ManagementJobCertificate
            {
                Alias = "test",
            },
            JobHistoryId = 1
        };

        // Act
        JobResult result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
        Assert.Equal(1, result.JobHistoryId);
        if (client.CertificatesAvailableOnFakeAppGateway != null)
        {
            Assert.False(client.CertificatesAvailableOnFakeAppGateway.ContainsKey("test"));
        }

        _logger.LogInformation("AzureAppGw_ManagementRemove_ProcessJob_ValidClient_ReturnSuccess - Success");
    }

    [Fact]
    public void AzureAppGw_ManagementRemove_ProcessJob_CertificateBoundToHttpsListener_ReturnFailure()
    {
        // Arrange
        FakeClient client = new FakeClient
        {
            CertificatesAvailableOnFakeAppGateway = new Dictionary<string, ApplicationGatewaySslCertificate>
            {
                { "test", new ApplicationGatewaySslCertificate { Name = "test", Password = "test-listener" } }
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
            OperationType = CertStoreOperationType.Remove,
            JobCertificate = new ManagementJobCertificate
            {
                Alias = "test",
            },
            JobHistoryId = 1
        };

        // Act
        JobResult result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal(1, result.JobHistoryId);

        _logger.LogInformation("AzureAppGw_ManagementRemove_ProcessJob_CertificateBoundToHttpsListener_ReturnFailure - Success");
    }

    [Fact]
    public void AzureAppGw_ManagementReplace_ProcessJob_CertificateIsBoundToHttpsListener_ReturnSuccess()
    {
        // Arrange
        FakeClient client = new FakeClient
        {
            CertificatesAvailableOnFakeAppGateway = new Dictionary<string, ApplicationGatewaySslCertificate>
            {
                // The Password field is used to track certificates bound to HTTPS listeners
                { "test", new ApplicationGatewaySslCertificate { Name = "test", Data = BinaryData.FromObjectAsJson("original-cert-data"), Password = "fake-https-listener" } }
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
                Alias = "test",
                Contents = "new-certificate-data",
                PrivateKeyPassword = "test-password"
            },
            JobHistoryId = 1
        };

        // Act
        JobResult result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
        Assert.Equal(1, result.JobHistoryId);
        if (client.CertificatesAvailableOnFakeAppGateway != null)
        {
            Assert.True(client.CertificatesAvailableOnFakeAppGateway.ContainsKey("test"));
            Assert.Equal(BinaryData.FromObjectAsJson("original-cert-data").ToString(), client.CertificatesAvailableOnFakeAppGateway["test"].Data.ToString());
        }

        _logger.LogInformation("AzureAppGw_ManagementReplace_ProcessJob_CertificateIsBoundToHttpsListener_ReturnSuccess");
    }

    [Fact]
    public void AzureAppGw_ManagementReplace_ProcessJob_ValidClient_ReturnSuccess()
    {
        // Arrange
        FakeClient client = new FakeClient
        {
            CertificatesAvailableOnFakeAppGateway = new Dictionary<string, ApplicationGatewaySslCertificate>
            {
                { "test", new ApplicationGatewaySslCertificate { Name = "test", Data = BinaryData.FromObjectAsJson("original-cert-data") } }
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
                Alias = "test",
                Contents = "new-certificate-data",
                PrivateKeyPassword = "test-password"
            },
            JobHistoryId = 1
        };

        // Act
        JobResult result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
        Assert.Equal(1, result.JobHistoryId);
        if (client.CertificatesAvailableOnFakeAppGateway != null)
        {
            Assert.True(client.CertificatesAvailableOnFakeAppGateway.ContainsKey("test"));
            Assert.Equal(BinaryData.FromObjectAsJson("new-certificate-data").ToString(), client.CertificatesAvailableOnFakeAppGateway["test"].Data.ToString());
        }

        _logger.LogInformation("AzureAppGw_ManagementReplace_ProcessJob_ValidClient_ReturnSuccess - Success");
    }

    [IntegrationTestingFact]
    public void AzureAppGw_Management_IntegrationTest_ReturnSuccess()
    {
        // Arrange
        IntegrationTestingFact env = new();

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
                Alias = certName,
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

        // Arrange

        ssCert = AzureAppGatewayOrchestrator_Client.GetSelfSignedCert(testHostname);

        b64PfxSslCert = Convert.ToBase64String(ssCert.Export(X509ContentType.Pfx, password));
        
        config.OperationType = CertStoreOperationType.Add;
        config.Overwrite = true;
        config.JobCertificate = new ManagementJobCertificate
        {
            Alias = certName,
            Contents = b64PfxSslCert,
            PrivateKeyPassword = password
        };

        // Act
        // This will process a Management Replace job
        result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);

        // Arrange
        config.OperationType = CertStoreOperationType.Remove;
        config.JobCertificate = new ManagementJobCertificate
        {
            Alias = certName,
        };

        // Act
        // This will process a Management Remove job
        result = management.ProcessJob(config);

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
