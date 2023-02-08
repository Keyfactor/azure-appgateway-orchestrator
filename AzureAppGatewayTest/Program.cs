﻿// Copyright 2023 Keyfactor
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

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Azure.Core;
using Azure.ResourceManager.Network.Models;
using Keyfactor.Extensions.Orchestrator.AzureAppGateway.Client;
using Keyfactor.Orchestrators.Extensions;

namespace AzureAppGatewayTest
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Program p = new Program();
            
            p.TestGetCertificates();
            string name = p.TestAddCertificate();
            p.RemoveCertificate(name);
        }

        public Program()
        {
            AzureProperties properties = new AzureProperties()
            {
                TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? string.Empty,
                ApplicationId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? string.Empty,
                ClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? string.Empty
            };

            Client = new AzureAppGatewayClient(properties)
            {
                AppGatewayResourceId = new ResourceIdentifier(Environment.GetEnvironmentVariable("AZURE_GATEWAY_RESOURCE_ID") ?? string.Empty)
            };
        }

        private AzureAppGatewayClient Client { get; }

        public void TestGetCertificates()
        {
            Console.Write("Getting App Gateway Certificates...\n");
            foreach (CurrentInventoryItem certInv in Client.GetAppGatewaySslCertificates())
            {
                Console.Write($"    Found certificate called {certInv.Alias}\n");
            }
        }

        public string TestAddCertificate()
        {
            // Generate random name for hostname and certificate name
            string certName = "GatewayTest" + Guid.NewGuid().ToString().Substring(0, 6);
            X509Certificate2 ssCert = GetSelfSignedCert(certName);
            string b64PfxSsCert = Convert.ToBase64String(ssCert.Export(X509ContentType.Pfx, "password"));
            Console.Write("Adding App Gateway Certificate...\n");
            ApplicationGatewaySslCertificate certObject = Client.AddAppGatewaySslCertificate(certName, b64PfxSsCert, "password");
            
            //Client.UpdateAppGatewayListenerCertificate(certObject, "routing-listener1");
            return certName;
        }
        
        public X509Certificate2 GetSelfSignedCert(string hostname)
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
            Console.Write($"Created self-signed certificate for {hostname} with thumbprint {selfSignedCert.Thumbprint}\n");
            return selfSignedCert;
        }
        
        public void RemoveCertificate(string certName)
        {
            Console.Write("Removing App Gateway Certificate...\n");
            Client.RemoveAppGatewaySslCertificate(certName);
        }
    }
}
