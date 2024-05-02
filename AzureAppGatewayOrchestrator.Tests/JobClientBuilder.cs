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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AzureAppGatewayOrchestrator.Tests;
using AzureApplicationGatewayOrchestratorExtension;
using AzureApplicationGatewayOrchestratorExtension.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

public class JobClientBuilder
{
    ILogger _logger { get; set;}

    public JobClientBuilder()
    {
        ConfigureLogging();

        _logger = LogHandler.GetClassLogger<JobClientBuilder>();
    }

    [Fact]
    public void AppGatewayJobClientBuilder_ValidCertificateStoreConfigClientSecret_BuildValidClient()
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

        _logger.LogInformation("AppGatewayJobClientBuilder_ValidCertificateStoreConfigClientSecret_BuildValidClient - Success");
    }

    [IntegrationTestingTheory]
    [InlineData("pkcs12")]
    [InlineData("pem")]
    [InlineData("encryptedPem")]
    public void AppGatewayJobClientBuilder_ValidCertificateStoreConfigClientCertificate_BuildValidClient(string certificateFormat)
    {
        // Verify that the AppGatewayJobClientBuilder uses the certificate store configuration
        // provided by Keyfactor Command/the Universal Orchestrator correctly as required
        // by the IAzureGraphClientBuilder interface.

        // Arrange
        AppGatewayJobClientBuilder<FakeClient.FakeBuilder> jobClientBuilderWithFakeBuilder = new();

        string password = "passwordpasswordpassword";
        string certName = "SPTest" + Guid.NewGuid().ToString()[..6];
        X509Certificate2 ssCert = Client.GetSelfSignedCert(certName);

        string b64ClientCertificate;
        if (certificateFormat == "pkcs12")
        {
            b64ClientCertificate = Convert.ToBase64String(ssCert.Export(X509ContentType.Pfx, password));
        }
        else if (certificateFormat == "pem")
        {
            string pemCert = ssCert.ExportCertificatePem();
            string keyPem = ssCert.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem();
            b64ClientCertificate = Convert.ToBase64String(Encoding.UTF8.GetBytes(keyPem + '\n' + pemCert));
            password = "";
        }
        else
        {
            PbeParameters pbeParameters = new PbeParameters(
                    PbeEncryptionAlgorithm.Aes256Cbc,
                    HashAlgorithmName.SHA384,
                    300_000);
            string pemCert = ssCert.ExportCertificatePem();
            string keyPem = ssCert.GetRSAPrivateKey()!.ExportEncryptedPkcs8PrivateKeyPem(password.ToCharArray(), pbeParameters);
            b64ClientCertificate = Convert.ToBase64String(Encoding.UTF8.GetBytes(keyPem + '\n' + pemCert));
        }

        // Set up the certificate store with names that correspond to how we expect them to be interpreted by
        // the builder
        CertificateStore fakeCertificateStoreDetails = new()
        {
            ClientMachine = "fake-tenant-id",
            StorePath = "fake-azure-resource-id",
            Properties = $@"{{""ServerUsername"": ""fake-azure-application-id"",""ServerPassword"": ""{password}"",""ClientCertificate"": ""{b64ClientCertificate}"",""AzureCloud"": ""fake-azure-cloud""}}"
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
        Assert.Equal("fake-azure-cloud", jobClientBuilderWithFakeBuilder._builder._azureCloudEndpoint);
        Assert.Equal(ssCert.GetCertHash(), jobClientBuilderWithFakeBuilder._builder._clientCertificate!.GetCertHash());
        Assert.NotNull(jobClientBuilderWithFakeBuilder._builder._clientCertificate!.GetRSAPrivateKey());
        Assert.Equal(jobClientBuilderWithFakeBuilder._builder._clientCertificate!.GetRSAPrivateKey()!.ExportRSAPrivateKeyPem(), ssCert.GetRSAPrivateKey()!.ExportRSAPrivateKeyPem());

        _logger.LogInformation("AppGatewayJobClientBuilder_ValidCertificateStoreConfigClientCertificate_BuildValidClient - Success");
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
