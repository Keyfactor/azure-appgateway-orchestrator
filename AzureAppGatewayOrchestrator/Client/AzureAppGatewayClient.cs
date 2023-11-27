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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.AzureAppGateway.Client
{
    public class AzureAppGatewayClient
    {
        private ILogger _logger { get; }

        private AzureProperties _jobProperties { get; set; }

        private ResourceIdentifier? _appGatewayResourceId { get; set; }

        public AzureAppGatewayClient(AzureProperties properties)
        {
            _jobProperties = properties;
            _logger = LogHandler.GetClassLogger<AzureAppGatewayClient>();
            _logger.LogDebug("Initializing Azure App Services client");            
            _appGatewayResourceId = properties.StorePath != null ? new ResourceIdentifier(properties.StorePath) : null;
        }

        private ApplicationGatewayCollection GetAppGatewayParentCollection()
        {
            // Use subscription resource to get resource group resource that contains App Gateway
            var subscriptionResourceIdentifier = new ResourceIdentifier(_appGatewayResourceId.SubscriptionId);
            var subscriptionResource = _armClient.GetSubscriptionResource(subscriptionResourceIdentifier);
            ResourceGroupResource resourceGroupResource = subscriptionResource.GetResourceGroup(_appGatewayResourceId.ResourceGroupName);
            return resourceGroupResource.GetApplicationGateways();
        }

        internal protected virtual ArmClient getArmClient(string tenantId)
        {
            TokenCredential credential;
            var credentialOptions = new DefaultAzureCredentialOptions { AuthorityHost = _jobProperties.AzureCloudEndpoint, AdditionallyAllowedTenants = { "*" } };
            {
                _logger.LogTrace("getting credentials for a service principal identity");
                credential = new ClientSecretCredential(tenantId, _jobProperties.ApplicationId, _jobProperties.ClientSecret, credentialOptions);
                _logger.LogTrace("got credentials for service principal identity", credential);
            }

            _mgmtClient = new ArmClient(credential);
            _logger.LogTrace("created management client", _mgmtClient);
            return _mgmtClient;
        }

        internal protected virtual ArmClient _armClient
        {
            get
            {
                if (_mgmtClient != null)
                {
                    _logger.LogTrace("getting previously initialized management client");
                    return _mgmtClient;
                }
                return getArmClient(_jobProperties.TenantId);
            }
        }

        protected virtual ArmClient _mgmtClient { get; set; }

        public IEnumerable<CurrentInventoryItem> GetAppGatewaySslCertificates()
        {
            ApplicationGatewayResource appGatewayResource =
                _armClient.GetApplicationGatewayResource(_appGatewayResourceId).Get();
            _logger.LogDebug($"Getting SSL certificates from App Gateway called \"{appGatewayResource.Data.Name}\"");
            _logger.LogDebug($"There are {appGatewayResource.Data.SslCertificates.Count()} certificates in the response.");
            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

            foreach (ApplicationGatewaySslCertificate certObject in appGatewayResource.Data.SslCertificates)
            {
                // ApplicationGatewaySslCertificate is in PKCS#7 format

                // Azure returns public cert data wrapped in parentheses. Remove them.
                byte[] untrimmedCertBytes = certObject.PublicCertData.ToArray();
                byte[] b64CertBytes = untrimmedCertBytes.Skip(1).Take(untrimmedCertBytes.Length - 2).ToArray();

                SignedCms pkcs7 = new SignedCms();
                //pkcs7.Decode(Encoding.UTF8.GetBytes(pkcs7CertPemString));
                pkcs7.Decode(Convert.FromBase64String(Encoding.UTF8.GetString(b64CertBytes)));

                // Create new inventory item for the certificate
                List<string> list = new List<string>();
                foreach (X509Certificate2 cert in pkcs7.Certificates)
                {
                    list.Add(Convert.ToBase64String(cert.RawData));
                }

                CurrentInventoryItem inventoryItem = new CurrentInventoryItem()
                {
                    Alias = certObject.Name,
                    PrivateKeyEntry = false,
                    ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                    UseChainLevel = true,
                    Certificates = list
                };
                _logger.LogDebug($"Found certificate called \"{certObject.Name}\" ({certObject.Id})");
                inventoryItems.Add(inventoryItem);
            }
            _logger.LogDebug($"Found {inventoryItems.Count()} certificates in app gateway");
            return inventoryItems;
        }

        public ApplicationGatewaySslCertificate AddAppGatewaySslCertificate(string certificateName, string certificateData, string certificatePassword, string httpListenerName = "")
        {
            ApplicationGatewayResource appGatewayResource =
                _armClient.GetApplicationGatewayResource(_appGatewayResourceId).Get();
            _logger.LogDebug($"Adding SSL certificate to App Gateway called \"{appGatewayResource.Data.Name}\"");

            // Create new certificate object with certificate data
            ApplicationGatewaySslCertificate gatewaySslCertificate = new ApplicationGatewaySslCertificate
            {
                Id = null,
                Name = certificateName,
                Data = BinaryData.FromObjectAsJson(certificateData),
                Password = certificatePassword
            };

            // Add the new gateway certificate to the already retrieved App Gateway resource
            appGatewayResource.Data.SslCertificates.Add(gatewaySslCertificate);

            // Update the App Gateway resource
            appGatewayResource = GetAppGatewayParentCollection().CreateOrUpdate(WaitUntil.Completed, appGatewayResource.Data.Name, appGatewayResource.Data).WaitForCompletion();
            _logger.LogDebug($"Added SSL certificate called \"{certificateName}\" to App Gateway called \"{appGatewayResource.Data.Name}\"");

            ApplicationGatewaySslCertificate certificateObject = appGatewayResource.Data.SslCertificates.FirstOrDefault(cert => cert.Name == certificateName);

            // If we don't have a listener name, we don't need to update the listener.
            if (string.IsNullOrEmpty(httpListenerName)) return certificateObject;

            try
            {
                // Otherwise, we need to update the listener with the new certificate.
                UpdateAppGatewayListenerCertificate(certificateObject, httpListenerName);
            }
            catch (Exception)
            {
                // If we fail to update the listener, we want to remove the certificate from the gateway.
                // Otherwise, we'll have a certificate in the gateway that isn't being used and existing
                // in a limbo state.
                _logger.LogWarning("Failed to update listener with new certificate. Removing certificate from App Gateway.");
                RemoveAppGatewaySslCertificate(certificateName);
                throw;
            }

            return certificateObject;
        }

        public void RemoveAppGatewaySslCertificate(string certificateName, string replacementHttpListenerCertificateName = "")
        {
            ApplicationGatewayResource appGatewayResource =
                _armClient.GetApplicationGatewayResource(_appGatewayResourceId).Get();
            _logger.LogDebug($"Removing SSL certificate called \"{certificateName}\" from App Gateway called \"{appGatewayResource.Data.Name}\"");

            // Find the certificate object called certificateName
            ApplicationGatewaySslCertificate gatewaySslCertificate = appGatewayResource.Data.SslCertificates.FirstOrDefault(c => c.Name == certificateName);
            if (gatewaySslCertificate == null)
            {
                _logger.LogDebug($"Certificate called \"{certificateName}\" not found in App Gateway called \"{appGatewayResource.Data.Name}\"");
                return;
            }

            // Determine if the certificate is in use by any listeners
            ApplicationGatewayHttpListener listener = appGatewayResource.Data.HttpListeners.FirstOrDefault(l => l.SslCertificateId == gatewaySslCertificate.Id);
            if (listener != null)
            {
                // If a listener is using the certificate, reassign it to another certificate
                // If no replacement certificate name is specified, use the first available certificate
                ApplicationGatewaySslCertificate newCertificate;
                if (string.IsNullOrEmpty(replacementHttpListenerCertificateName))
                {
                    newCertificate = appGatewayResource.Data.SslCertificates.FirstOrDefault(c =>
                        c.Name != certificateName);
                }
                else
                {

                    newCertificate = appGatewayResource.Data.SslCertificates.FirstOrDefault(c =>
                        c.Name == replacementHttpListenerCertificateName);
                }

                if (newCertificate == null)
                {
                    string error = $"Certificate called \"{certificateName}\" is in use by listener \"{listener.Name}\" and no other certificates are available for reassignment.";
                    _logger.LogError(error);
                    throw new Exception(error);
                }
                // Reassign the listener to use the new certificate
                _logger.LogDebug($"Certificate called \"{certificateName}\" is in use by listener \"{listener.Name}\". Reassigning listener to use certificate called \"{newCertificate.Name}\"");
                UpdateAppGatewayListenerCertificate(newCertificate, listener.Name);

                // If update succeeded, appGatewayResource is out of date. Get it again.
                appGatewayResource = _armClient.GetApplicationGatewayResource(_appGatewayResourceId).Get();
                gatewaySslCertificate = appGatewayResource.Data.SslCertificates.FirstOrDefault(c => c.Name == certificateName);
            }

            // Remove the certificate object from the App Gateway resource
            appGatewayResource.Data.SslCertificates.Remove(gatewaySslCertificate);

            // Update the App Gateway resource
            GetAppGatewayParentCollection().CreateOrUpdate(WaitUntil.Completed, appGatewayResource.Data.Name, appGatewayResource.Data);

            _logger.LogDebug($"Successfully removed SSL certificate called \"{certificateName}\" from App Gateway called \"{appGatewayResource.Data.Name}\"");
        }

        public void ReplaceAppGatewayCertificate(string certificateName, string certificateData, string certificatePassword)
        {
            _logger.LogDebug($"Replacing SSL certificate called \"{certificateName}\" in App Gateway called \"{_appGatewayResourceId.Name}\"");
            // If the certificate exists, we want to remove it and add it again
            string tempAlias = "";
            string httpListenerName = AppGatewaySslCertificateIsAttachedToListener(certificateName);
            if (AppGatewaySslCertificateExists(certificateName))
            {
                _logger.LogDebug($"Certificate called \"{certificateName}\" already exists\". Replacing it.");
                // First, add the certificate to the App Gateway under a random alias
                tempAlias = Guid.NewGuid().ToString();

                // Specify the listener name so that the certificate is assigned to the listener
                _logger.LogDebug($"Adding temporary certificate called \"{tempAlias}\"");

                AddAppGatewaySslCertificate(tempAlias, certificateData, certificatePassword, httpListenerName);

                // Remove the certificate with the original alias
                _logger.LogDebug($"Removing original certificate called \"{certificateName}\"");
                RemoveAppGatewaySslCertificate(certificateName);
            }

            // Add the certificate with the original alias
            AddAppGatewaySslCertificate(certificateName, certificateData, certificatePassword, httpListenerName);

            if (string.IsNullOrEmpty(tempAlias)) return;

            // At this point, the temporary certificate shouldn't be assigned to any listeners
            _logger.LogDebug($"Removing temporary certificate called \"{tempAlias}\"");
            RemoveAppGatewaySslCertificate(tempAlias);
        }

        public void UpdateAppGatewayListenerCertificate(ApplicationGatewaySslCertificate certificate, string listenerName)
        {
            ApplicationGatewayResource appGatewayResource = _armClient.GetApplicationGatewayResource(_appGatewayResourceId).Get();

            // First, verify that certificate exists in App Gateway
            if (!AppGatewaySslCertificateExists(certificate.Name))
            {
                string error =
                    $"Certificate with name \"{certificate.Name}\" does not exist in App Gateway \"{appGatewayResource.Data.Name}\"";
                _logger.LogError(error);
                throw new Exception(error);
            }

            // Find the listener object called listenerName and update its certificate
            ApplicationGatewayHttpListener listener =
                appGatewayResource.Data.HttpListeners.FirstOrDefault(l => l.Name == listenerName);
            if (listener != null) listener.SslCertificateId = certificate.Id;
            else
            {
                string error =
                    $"Listener with name \"{listenerName}\" does not exist in App Gateway \"{appGatewayResource.Data.Name}\"";
                _logger.LogError(error);
                throw new Exception(error);
            }
            _logger.LogDebug($"Updating listener \"{listenerName}\" to use certificate \"{certificate.Name}\"");

            // Update the App Gateway resource
            appGatewayResource.Data.HttpListeners.Remove(appGatewayResource.Data.HttpListeners.FirstOrDefault(l => l.Name == listenerName));
            appGatewayResource.Data.HttpListeners.Add(listener);

            GetAppGatewayParentCollection().CreateOrUpdate(WaitUntil.Completed, appGatewayResource.Data.Name, appGatewayResource.Data);

            _logger.LogDebug($"Successfully updated listener \"{listenerName}\" with certificate called \"{certificate.Name}\".");
        }

        public bool AppGatewaySslCertificateExists(string certificateName)
        {
            ApplicationGatewayResource appGatewayResource = _armClient.GetApplicationGatewayResource(_appGatewayResourceId).Get();
            return appGatewayResource.Data.SslCertificates.FirstOrDefault(c => c.Name == certificateName) != null;
        }

        public string AppGatewaySslCertificateIsAttachedToListener(string certificateName)
        {
            ApplicationGatewayResource appGatewayResource = _armClient.GetApplicationGatewayResource(_appGatewayResourceId).Get();
            ApplicationGatewaySslCertificate certificate = appGatewayResource.Data.SslCertificates.FirstOrDefault(c => c.Name == certificateName);
            if (certificate == null) return "";
            ApplicationGatewayHttpListener listener = appGatewayResource.Data.HttpListeners.FirstOrDefault(l => l.SslCertificateId == certificate.Id);
            return listener?.Name;
        }

        public IEnumerable<string> DiscoverAppGateways()
        {
            var appGatewayResourceIds = new List<string>();

            _jobProperties.TenantIdsForDiscovery.ForEach(tenantId =>
            {
                try
                {
                    // create a new ArmClient for each tenant..
                    var armClient = getArmClient(tenantId);
                    var subscriptions = armClient.GetSubscriptions();
                    // for each subscription in that tenant.. 
                    foreach (var subscriptionResource in subscriptions)
                    {
                        _logger.LogDebug($"Searching for Application Gateways in tenant ID {tenantId} and subscription ID {subscriptionResource.Data.SubscriptionId}");
                        // get all of the application gateways
                        var appGateways = subscriptionResource.GetApplicationGateways().Select(ag => ag.Data.Id.ToString());
                        appGatewayResourceIds.AddRange(appGateways);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error performing discovery on tenantId {tenantId}");
                }
            });

            _logger.LogDebug($"Discovered {appGatewayResourceIds.Count()} App Gateways");

            return appGatewayResourceIds;
        }
    }
}
