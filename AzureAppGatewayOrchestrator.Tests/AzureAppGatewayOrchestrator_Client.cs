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
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Mocking;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using AzureApplicationGatewayOrchestratorExtension.Client;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using Moq;
using NLog.Extensions.Logging;

namespace AzureAppGatewayOrchestrator.Tests;

public class AzureAppGatewayOrchestrator_Client
{
    private ResourceIdentifier _appGatewayResourceId;
    
    public AzureAppGatewayOrchestrator_Client()
    {
        ConfigureLogging();
        
        _appGatewayResourceId = new ResourceIdentifier("/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/testResourceGroup/providers/Microsoft.Network/applicationGateways/testAppGateway");
    }
    
    [IntegrationTestingFact]
    public void AzureClientIntegrationTest()
    {
        // Arrange
        string httpsListenerName = Environment.GetEnvironmentVariable("AZURE_APP_GATEWAY_HTTPS_LISTENER_NAME") ?? string.Empty;

        IAzureAppGatewayClient client = new GatewayClient.Builder()
            .WithTenantId(Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? string.Empty)
            .WithApplicationId(Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? string.Empty)
            .WithClientSecret(Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? string.Empty)
            .WithResourceId(Environment.GetEnvironmentVariable("AZURE_APP_GATEWAY_RESOURCE_ID") ?? string.Empty)
            .Build();
    
        string certName = "GatewayTest" + Guid.NewGuid().ToString()[..6];
        string password = "password";

        X509Certificate2 ssCert = GetSelfSignedCert(certName);
        string b64PfxSslCert = Convert.ToBase64String(ssCert.Export(X509ContentType.Pfx, password));
        
        // Step 1 - Add an App Gateway certificate

        // Act
        ApplicationGatewaySslCertificate result = client.AddCertificate(certName, b64PfxSslCert, password);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(certName, result.Name);

        // Step 2 - Update an HTTPS listener with the new certificate
        
        // Act
        bool ex = false;
        try
        {
            client.UpdateHttpsListenerCertificate(result, httpsListenerName);
        }
        catch (Exception)
        {
            ex = true;
        }

        // Assert
        Assert.False(ex);

        // Step 3 - Get the certificates that exist on the app gateway

        // Act
        OperationResult<IEnumerable<Keyfactor.Orchestrators.Extensions.CurrentInventoryItem>> certs = client.GetAppGatewaySslCertificates();

        // Assert
        Assert.NotNull(certs.Result);
        Assert.NotEmpty(certs.Result);
        Assert.Contains(certs.Result, c => c.Alias == certName);

        // Step 4 - Try to remove the certificate from the app gateway, which should fail 
        // since it's bound to an HTTPS listener

        // Act
        ex = false;
        try
        {
            // Client should throw exception if certificate is bound to an HTTPS listener.
            client.RemoveCertificate(certName);
        }
        catch (Exception)
        {
            ex = true;
        }

        // Assert
        Assert.True(ex);

        // Act
        
        // Step 5 - Remove the certificate from the HTTPS listener
        
        // Rebind the HTTPS listener with the original certificate, if there was only 1 certificate
        // previously, otherwise bind it with the first certificate in the list.

        ApplicationGatewaySslCertificate replacement = client.GetAppGatewayCertificateByName(certs.Result.First(c => c.Alias != certName).Alias);

        client.UpdateHttpsListenerCertificate(replacement, httpsListenerName);

        client.RemoveCertificate(certName);

        // Act

        // Step 6 - Test the GetHttpsListenerCertificates method

        // boundCertificates in the form of <listenerName, certificateName>
        IDictionary<string, string> boundCertificates = client.GetBoundHttpsListenerCertificates();
    }

    public static X509Certificate2 GetSelfSignedCert(string hostname)
    {
        RSA rsa = RSA.Create(2048);
        CertificateRequest req = new CertificateRequest($"CN={hostname}", rsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        SubjectAlternativeNameBuilder subjectAlternativeNameBuilder = new SubjectAlternativeNameBuilder();
        subjectAlternativeNameBuilder.AddDnsName(hostname);
        req.CertificateExtensions.Add(subjectAlternativeNameBuilder.Build());
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));        
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("2.5.29.32.0"), new Oid("1.3.6.1.5.5.7.3.1") }, false));

        X509Certificate2 selfSignedCert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));
        Console.Write($"Created self-signed certificate for \"{hostname}\" with thumbprint {selfSignedCert.Thumbprint}\n");
        return selfSignedCert;
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

    public class MockArmClientBuilder
    {
        private Mock<ArmClient> _armClientMock = new();
        private Mock<MockableNetworkArmClient> _mockableNetworkArmClient = new();
        private Mock<ApplicationGatewayResource> _appGatewayResourceMock = new();

        public MockArmClientBuilder HookUpGetApplicationGatewayResource(ResourceIdentifier appGatewayResourceId, ApplicationGatewayData appGatewayData)
        {
            // Mock the ApplicationGatewayResource method
            _appGatewayResourceMock.SetupGet(r => r.Data).Returns(appGatewayData);

            _mockableNetworkArmClient.Setup(client => client.GetApplicationGatewayResource(appGatewayResourceId))
                .Returns(_appGatewayResourceMock.Object);

            // Create a Response object to return from Get()
            Response<ApplicationGatewayResource> appGatewayResponseMock = Response.FromValue(_appGatewayResourceMock.Object, Mock.Of<Response>());

            // Hook up the Get() method to return the mock response
            _appGatewayResourceMock.Setup(resource => resource.Get(CancellationToken.None))
                .Returns(appGatewayResponseMock);

            return this;
        }

        public MockArmClientBuilder HookUpGatewayCollectionGetter()
        {
            // Set up the ApplicationGatewayCollection operations so they can be hooked in later
            var mockApplicationGatewayCollection = new Mock<ApplicationGatewayCollection>();
            
            // Create a Mock ArmOperation that will be populated with the contents of the AppGateway the client
            // is trying to update
            var mockArmOperation = new Mock<ArmOperation<ApplicationGatewayResource>>();

            mockApplicationGatewayCollection.Setup(c => c.CreateOrUpdate(
                        It.IsAny<WaitUntil>(),
                        It.IsAny<string>(),
                        It.IsAny<ApplicationGatewayData>(),
                        It.IsAny<CancellationToken>()))
                .Callback<WaitUntil, string, ApplicationGatewayData, CancellationToken>((waitUntil, name, data, token) =>
                        {
                        // Use 'data' to set up your mockArmOperation
                        var mockAppGatewayResource = new Mock<ApplicationGatewayResource>();
                        mockAppGatewayResource.SetupGet(r => r.Data).Returns(data);

                        mockArmOperation.Setup(op => op.Value).Returns(mockAppGatewayResource.Object);
                        })
                .Returns(mockArmOperation.Object);

            var mockSubscriptionResource = new Mock<SubscriptionResource>();
            var mockableNetworkSubscriptionResource = new Mock<MockableNetworkSubscriptionResource>();
            var mockResourceGroupResource = new Mock<ResourceGroupResource>();
            var mockableNetworkResourceGroupResource = new Mock<MockableNetworkResourceGroupResource>();

            // Hook up the Mock SubscriptionResource to the Mock ArmClient
            _armClientMock.Setup(client => client.GetSubscriptionResource(It.IsAny<ResourceIdentifier>()))
                .Returns(mockSubscriptionResource.Object);

            // Set up GetSubscriptionResource to return the mock subscription resource
            mockableNetworkResourceGroupResource.Setup(rg => rg.GetApplicationGateways())
                .Returns(Mock.Of<ApplicationGatewayCollection>());

            // Create a mock response for GetResourceGroup
            var mockResourceGroupResponse = Response.FromValue(mockResourceGroupResource.Object, Mock.Of<Response>());

            // Set up GetResourceGroup to return the mock response
            mockSubscriptionResource.Setup(sr => sr.GetResourceGroup(It.IsAny<string>(), CancellationToken.None))
                .Returns(mockResourceGroupResponse);

            // Hooking up MockableNetworkResourceGroupResource with ResourceGroupResource
            mockResourceGroupResource.Setup(rg => rg.GetCachedClient(
                        It.IsAny<Func<ArmClient, MockableNetworkResourceGroupResource>>()))
                .Returns(mockableNetworkResourceGroupResource.Object);

            // Similar setup for SubscriptionResource and MockableNetworkSubscriptionResource
            mockSubscriptionResource.Setup(sr => sr.GetCachedClient(
                        It.IsAny<Func<ArmClient, MockableNetworkSubscriptionResource>>()))
                .Returns(mockableNetworkSubscriptionResource.Object);
        
            return this;
        }

        public Mock<ArmClient> Build()
        {
            // Mock the GetCachedClient method on ArmClient
            _armClientMock.Setup(client => client.GetCachedClient(
                        It.IsAny<Func<ArmClient, MockableNetworkArmClient>>()))
                .Returns(_mockableNetworkArmClient.Object);

            return _armClientMock;
        }
    }

    private void WillFinishEventually()
    {
        // Arrange
        string certificateName = "testCert";
        string b64Pkcs12Certificate = "base64CertificateData";
        string password = "testPassword";
        
        ApplicationGatewayData appGatewayData = new ApplicationGatewayData();

        GatewayClient client = new GatewayClient(new MockArmClientBuilder()
            .HookUpGetApplicationGatewayResource(_appGatewayResourceId, appGatewayData)
            .HookUpGatewayCollectionGetter()
            .Build().Object);
        client.AppGatewayResourceId = _appGatewayResourceId;

        ApplicationGatewaySslCertificate result = client.AddCertificate(certificateName, b64Pkcs12Certificate, password);
    }
}

