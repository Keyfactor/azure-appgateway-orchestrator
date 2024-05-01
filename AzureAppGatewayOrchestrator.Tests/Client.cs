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
using Azure.Core;
using Azure.ResourceManager.Network.Models;
using AzureApplicationGatewayOrchestratorExtension.Client;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace AzureAppGatewayOrchestrator.Tests;

public class Client
{
    private ResourceIdentifier _appGatewayResourceId;
    
    public Client()
    {
        ConfigureLogging();
        
        _appGatewayResourceId = new ResourceIdentifier("/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/testResourceGroup/providers/Microsoft.Network/applicationGateways/testAppGateway");
    }
    
    [IntegrationTestingTheory]
    [InlineData("clientcert")]
    [InlineData("clientsecret")]
    public void AzureClientIntegrationTest(string testAuthMethod)
    {
        // Arrange
        IntegrationTestingFact env = new();

        IAzureAppGatewayClientBuilder clientBuilder = new GatewayClient.Builder()
            .WithTenantId(env.TenantId)
            .WithApplicationId(env.ApplicationId)
            .WithResourceId(env.ResourceId);

        if (testAuthMethod == "clientcert")
        {
            clientBuilder.WithClientSecret(env.ClientSecret);
        }
        else
        {
            var cert = X509Certificate2.CreateFromPemFile(env.ClientCertificatePath);
            clientBuilder.WithClientCertificate(cert);
        }

        IAzureAppGatewayClient client = clientBuilder.Build();
    
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
            client.UpdateHttpsListenerCertificate(result, env.HttpsListenerName);
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

        client.UpdateHttpsListenerCertificate(replacement, env.HttpsListenerName);

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
}

