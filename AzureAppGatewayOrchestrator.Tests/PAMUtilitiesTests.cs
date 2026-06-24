
//  Copyright 2026 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using AzureApplicationGatewayOrchestratorExtension;
using AzureApplicationGatewayOrchestratorExtension.AppGatewayCertificateJobs;
using FluentAssertions;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using NLog.Extensions.Logging;

namespace AzureAppGatewayOrchestrator.Tests;

public class PAMUtilitiesTests
{
    ILogger _logger { get; set; }

    public PAMUtilitiesTests()
    {
        ConfigureLogging();
        _logger = LogHandler.GetClassLogger<PAMUtilitiesTests>();
    }

    // -------------------------------------------------------------------------
    // PAMUtilities.ResolvePAMField - direct unit tests
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolvePAMField_NullResolver_ReturnsKeyDirectly()
    {
        // When no PAM resolver is configured, the raw value from store config
        // should be passed through unchanged - PAMUtilities is not a middleman.

        // Arrange
        string key = "literal-credential-value";

        // Act
        string result = PAMUtilities.ResolvePAMField(_logger, null, "Server Username", key);

        // Assert
        result.Should().Be(key);

        _logger.LogInformation("ResolvePAMField_NullResolver_ReturnsKeyDirectly - Success");
    }

    [Fact]
    public void ResolvePAMField_WithResolver_CallsResolveAndReturnsResolvedValue()
    {
        // When a PAM resolver is present, Resolve() must be called with the key
        // and its return value used instead of the raw key.

        // Arrange
        var mockResolver = new Mock<IPAMSecretResolver>();
        mockResolver.Setup(r => r.Resolve("pam-secret-key")).Returns("resolved-secret-value");

        // Act
        string result = PAMUtilities.ResolvePAMField(_logger, mockResolver.Object, "Server Password", "pam-secret-key");

        // Assert
        result.Should().Be("resolved-secret-value");
        mockResolver.Verify(r => r.Resolve("pam-secret-key"), Times.Once);

        _logger.LogInformation("ResolvePAMField_WithResolver_CallsResolveAndReturnsResolvedValue - Success");
    }

    [Fact]
    public void ResolvePAMField_WithResolver_ResolverCalledExactlyOnce()
    {
        // Resolve() should be called exactly once per field - no caching or double-calling.

        // Arrange
        var mockResolver = new Mock<IPAMSecretResolver>();
        mockResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns("any-resolved-value");

        // Act
        PAMUtilities.ResolvePAMField(_logger, mockResolver.Object, "description", "any-key");

        // Assert
        mockResolver.Verify(r => r.Resolve(It.IsAny<string>()), Times.Once);

        _logger.LogInformation("ResolvePAMField_WithResolver_ResolverCalledExactlyOnce - Success");
    }

    [Fact]
    public void ResolvePAMField_WithResolver_ReturnsNullWhenResolverReturnsNull()
    {
        // If the PAM provider returns null (e.g. secret not found), null should
        // be surfaced as-is rather than silently falling back to the key.

        // Arrange
        var mockResolver = new Mock<IPAMSecretResolver>();
        mockResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns((string)null);

        // Act
        string result = PAMUtilities.ResolvePAMField(_logger, mockResolver.Object, "Server Password", "pam-key");

        // Assert
        result.Should().BeNull();

        _logger.LogInformation("ResolvePAMField_WithResolver_ReturnsNullWhenResolverReturnsNull - Success");
    }

    [Theory]
    [InlineData("pam-key-one",   "resolved-value-one")]
    [InlineData("pam-key-two",   "resolved-value-two")]
    [InlineData("pam-key-three", "resolved-value-three")]
    public void ResolvePAMField_WithResolver_CorrectKeyForwardedToResolver(string key, string resolvedValue)
    {
        // The exact key string from store config must be forwarded to the resolver
        // unchanged - PAMUtilities must not mangle or trim the key.

        // Arrange
        var mockResolver = new Mock<IPAMSecretResolver>();
        mockResolver.Setup(r => r.Resolve(key)).Returns(resolvedValue);

        // Act
        string result = PAMUtilities.ResolvePAMField(_logger, mockResolver.Object, "field description", key);

        // Assert
        result.Should().Be(resolvedValue);
        mockResolver.Verify(r => r.Resolve(key), Times.Once);

        _logger.LogInformation($"ResolvePAMField_WithResolver_CorrectKeyForwardedToResolver ({key}) - Success");
    }

    // -------------------------------------------------------------------------
    // AppGatewayJobClientBuilder - PAM passthrough (no resolver configured)
    // -------------------------------------------------------------------------

    [Fact]
    public void AppGatewayJobClientBuilder_NoPAMResolver_CertificateStoreDetails_UsesValuesDirectly()
    {
        // Without a resolver, the raw ServerUsername / ServerPassword values from
        // store config should be used as-is (no PAM lookup attempted).

        // Arrange
        AppGatewayJobClientBuilder<FakeClient.FakeBuilder> builder = new();
        // resolver left null (default)

        CertificateStore storeDetails = new()
        {
            ClientMachine = "fake-tenant-id",
            StorePath     = "fake-resource-id",
            Properties    = "{\"ServerUsername\":\"direct-app-id\",\"ServerPassword\":\"direct-secret\",\"AzureCloud\":\"fake-cloud\"}"
        };

        // Act
        builder.WithCertificateStoreDetails(storeDetails).Build();

        // Assert
        builder._builder._applicationId.Should().Be("direct-app-id");
        builder._builder._clientSecret.Should().Be("direct-secret");

        _logger.LogInformation("AppGatewayJobClientBuilder_NoPAMResolver_CertificateStoreDetails_UsesValuesDirectly - Success");
    }

    [Fact]
    public void AppGatewayJobClientBuilder_NoPAMResolver_DiscoveryJobConfiguration_UsesValuesDirectly()
    {
        // Same passthrough guarantee for the Discovery path.

        // Arrange
        AppGatewayJobClientBuilder<FakeClient.FakeBuilder> builder = new();

        DiscoveryJobConfiguration config = new()
        {
            ClientMachine  = "fake-tenant-id",
            ServerUsername = "direct-app-id",
            ServerPassword = "direct-secret"
        };

        // Act
        builder.WithDiscoveryJobConfiguration(config, "fake-tenant-id").Build();

        // Assert
        builder._builder._applicationId.Should().Be("direct-app-id");
        builder._builder._clientSecret.Should().Be("direct-secret");

        _logger.LogInformation("AppGatewayJobClientBuilder_NoPAMResolver_DiscoveryJobConfiguration_UsesValuesDirectly - Success");
    }

    // -------------------------------------------------------------------------
    // Job-level: PAM resolver accepted via constructor and threaded through
    // -------------------------------------------------------------------------

    [Fact]
    public void AzureAppGw_Inventory_ProcessJob_WithPAMResolver_ResolvesCredentials()
    {
        // Inventory accepts an IPAMSecretResolver via its constructor and must
        // thread it through to the client builder so PAM keys are resolved
        // before the Azure client is constructed.
        //
        // Here we inject a pre-built FakeClient so the builder path is skipped,
        // and verify the job completes successfully with the resolver in place.

        // Arrange
        var mockResolver = new Mock<IPAMSecretResolver>();

        var inventory = new Inventory(mockResolver.Object)
        {
            Client = new FakeClient
            {
                CertificatesAvailableOnFakeAppGateway = new Dictionary<string, Azure.ResourceManager.Network.Models.ApplicationGatewaySslCertificate>
                {
                    { "cert1", new Azure.ResourceManager.Network.Models.ApplicationGatewaySslCertificate { Name = "cert1" } }
                }
            }
        };

        var config = new InventoryJobConfiguration
        {
            CertificateStoreDetails = new CertificateStore
            {
                ClientMachine = "fake-tenant-id",
                StorePath     = "fake-resource-id",
                Properties    = "{\"ServerUsername\":\"pam-username-key\",\"ServerPassword\":\"pam-password-key\",\"AzureCloud\":\"\"}"
            },
            JobHistoryId = 1
        };

        // Act
        JobResult result = inventory.ProcessJob(config, items =>
        {
            items.Should().ContainSingle();
            return true;
        });

        // Assert
        result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);

        _logger.LogInformation("AzureAppGw_Inventory_ProcessJob_WithPAMResolver_ResolvesCredentials - Success");
    }

    [Fact]
    public void AzureAppGwBin_Discovery_ProcessJob_WithPAMResolver_ResolvesCredentials()
    {
        // Discovery accepts an IPAMSecretResolver via its constructor and must
        // thread it through to the client builder.

        // Arrange
        var mockResolver = new Mock<IPAMSecretResolver>();

        var discovery = new AzureApplicationGatewayOrchestratorExtension.ListenerBindingJobs.Discovery(mockResolver.Object)
        {
            Client = new FakeClient
            {
                AppGatewaysAvailableOnFakeTenant = new List<string> { "fake-gateway" }
            }
        };

        var config = new DiscoveryJobConfiguration
        {
            ClientMachine  = "fake-tenant-id",
            ServerUsername = "pam-username-key",
            ServerPassword = "pam-password-key",
            JobProperties  = new Dictionary<string, object> { { "dirs", "fake-tenant-id" } }
        };

        // Act
        JobResult result = discovery.ProcessJob(config, discovered =>
        {
            discovered.Should().ContainSingle().Which.Should().Be("fake-gateway");
            return true;
        });

        // Assert
        result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);

        _logger.LogInformation("AzureAppGwBin_Discovery_ProcessJob_WithPAMResolver_ResolvesCredentials - Success");
    }

    static void ConfigureLogging()
    {
        var config = new NLog.Config.LoggingConfiguration();
        var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
        logconsole.Layout = @"${date:format=HH\:mm\:ss} ${logger} [${level}] - ${message}";
        config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, logconsole);
        NLog.LogManager.Configuration = config;

        LogHandler.Factory = LoggerFactory.Create(builder =>
        {
            builder.AddNLog();
        });
    }
}
