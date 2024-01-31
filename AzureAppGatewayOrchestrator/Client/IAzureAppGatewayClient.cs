// Copyright 2023 Keyfactor
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

using System.Collections.Generic;
using Azure.ResourceManager.Network.Models;
using Keyfactor.Orchestrators.Extensions;

namespace AzureApplicationGatewayOrchestratorExtension.Client
{
    public interface IAzureAppGatewayClientBuilder
    {
        public IAzureAppGatewayClientBuilder WithTenantId(string tenantId);
        public IAzureAppGatewayClientBuilder WithResourceId(string resourceId);
        public IAzureAppGatewayClientBuilder WithApplicationId(string applicationId);
        public IAzureAppGatewayClientBuilder WithClientSecret(string clientSecret);
        public IAzureAppGatewayClientBuilder WithAzureCloud(string azureCloud);
        public IAzureAppGatewayClient Build();
    }

    public interface IAzureAppGatewayClient
    {
        public ApplicationGatewaySslCertificate AddCertificate(string certificateName, string certificateData, string certificatePassword);
        public void RemoveCertificate(string certificateName);
        public IEnumerable<CurrentInventoryItem> GetAppGatewaySslCertificates();
        public ApplicationGatewaySslCertificate GetAppGatewayCertificateByName(string certificateName);
        public bool CertificateExists(string certificateName);
        public IEnumerable<string> DiscoverApplicationGateways();

        public bool CertificateIsBoundToHttpsListener(string certificateName);
        public void UpdateHttpsListenerCertificate(ApplicationGatewaySslCertificate certificate, string listenerName);
        public IDictionary<string, string> GetBoundHttpsListenerCertificates();
    }
}
