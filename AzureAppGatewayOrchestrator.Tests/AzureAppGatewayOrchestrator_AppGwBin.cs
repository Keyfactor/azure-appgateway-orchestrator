
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

    [Fact]
    public void AppGwBin_Inventory_IntegrationTest_ReturnSuccess()
    {
        // Arrange
        var iTenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? string.Empty;
        var iApplicationId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? string.Empty;
        var iClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? string.Empty;
        var iResourceId = Environment.GetEnvironmentVariable("AZURE_APP_GATEWAY_RESOURCE_ID") ?? string.Empty;

        // Set up the inventory job configuration
        var config = new InventoryJobConfiguration
        {
            CertificateStoreDetails = new CertificateStore
            {
                ClientMachine = iTenantId,
                              StorePath = iResourceId,
                              Properties = $"{{\"ServerUsername\":\"{iApplicationId}\",\"ServerPassword\":\"{iClientSecret}\",\"AzureCloud\":\"\"}}"
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
    public void AppGwBin_ManagementAdd_ProcessJob_ValidClient_ReturnSuccess()
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
        }

        _logger.LogInformation("AppGwBin_ManagementAdd_ProcessJob_ValidClient_ReturnSuccess - Success");
    }

    [Fact]
    public void AppGwBin_ManagementReplace_ProcessJob_ValidClient_ReturnSuccess()
    {
        // Arrange
        FakeClient client = new FakeClient
        {
            CertificatesAvailableOnFakeAppGateway = new Dictionary<string, ApplicationGatewaySslCertificate>
            {
                // Internally, FakeClient tracks certificates bound to listeners with the Password property
                { "test-certificate", new ApplicationGatewaySslCertificate { Name = "test-certificate", Password = "fake-https-listener" } }
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
        }

        _logger.LogInformation("AppGwBin_ManagementReplace_ProcessJob_ValidClient_ReturnSuccess - Success");
    }

    [Fact]
    public void AppGwBin_Management_IntegrationTest_ReturnSuccess()
    {
        // Arrange
        var iTenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? string.Empty;
        var iApplicationId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? string.Empty;
        var iClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? string.Empty;
        var iResourceId = Environment.GetEnvironmentVariable("AZURE_APP_GATEWAY_RESOURCE_ID") ?? string.Empty;
        var iHttpsListenerName = Environment.GetEnvironmentVariable("AZURE_APP_GATEWAY_HTTPS_LISTENER_NAME") ?? string.Empty;

        // Items necessary for cleanup
        IAzureAppGatewayClient client = new GatewayClient.Builder()
            .WithTenantId(iTenantId)
            .WithApplicationId(iApplicationId)
            .WithClientSecret(iClientSecret)
            .WithResourceId(iResourceId)
            .Build();

        IDictionary<string, string> currentlyBoundCertificates = client.GetBoundHttpsListenerCertificates();
        string currentlyBoundCertificate = currentlyBoundCertificates[iHttpsListenerName];
        _logger.LogTrace($"Certificate called {currentlyBoundCertificate} is currently bound to HTTPS listener {iHttpsListenerName}");

        string testHostname = "example.com";
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
                ClientMachine = iTenantId,
                              StorePath = iResourceId,
                              Properties = $"{{\"ServerUsername\":\"{iApplicationId}\",\"ServerPassword\":\"{iClientSecret}\",\"AzureCloud\":\"\"}}"
            },
            JobCertificate = new ManagementJobCertificate
            {
                Alias = iHttpsListenerName,
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
            Alias = iHttpsListenerName,
            Contents = b64PfxSslCert,
            PrivateKeyPassword = password
        };

        // Act
        // This will process a Management Replace job
        result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);

        // Cleanup
        ApplicationGatewaySslCertificate oldAppGatewayCertificate = client.GetAppGatewayCertificateByName(currentlyBoundCertificate);
        client.UpdateHttpsListenerCertificate(oldAppGatewayCertificate, iHttpsListenerName);
        client.RemoveCertificate(iHttpsListenerName);

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
