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
using Azure.Security.KeyVault.Secrets;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace AzureApplicationGatewayOrchestratorExtension.Client;

public class GatewayClient : IAzureAppGatewayClient
{
    private ILogger _logger { get; set; }
    private ArmClient _armClient { get; set; }
    private TokenCredential _credential { get; set; }
    public ResourceIdentifier AppGatewayResourceId { get; set; }

    private IDictionary<string, string> currentlyBoundAppGatewayCertificateCache { get; set; }

    // The Client can only be constructed by the Builder method
    // unless they use the constructor that passes a pre-configured
    // ArmClient object. This is to ensure that the Client is always
    // constructed with a valid ArmClient object.
    private GatewayClient()
    {
        _logger = LogHandler.GetClassLogger<GatewayClient>();
    }

    public GatewayClient(ArmClient armClient)
    {
        _logger = LogHandler.GetClassLogger<GatewayClient>();
        _armClient = armClient;
    }

    public class Builder : IAzureAppGatewayClientBuilder
    {
        private GatewayClient _client = new GatewayClient();

        private string _tenantId { get; set; }
        private string _resourceId { get; set; }
        private string _applicationId { get; set; }
        private string _clientSecret { get; set; }
        private X509Certificate2 _clientCertificate { get; set; }
        private Uri _azureCloudEndpoint { get; set; }

        public IAzureAppGatewayClientBuilder WithTenantId(string tenantId)
        {
            _tenantId = tenantId;
            return this;
        }

        public IAzureAppGatewayClientBuilder WithResourceId(string resourceId)
        {
            _resourceId = resourceId;
            return this;
        }

        public IAzureAppGatewayClientBuilder WithApplicationId(string applicationId)
        {
            _applicationId = applicationId;
            return this;
        }

        public IAzureAppGatewayClientBuilder WithClientSecret(string clientSecret)
        {
            _clientSecret = clientSecret;
            return this;
        }

        public IAzureAppGatewayClientBuilder WithClientCertificate(X509Certificate2 clientCertificate)
        {
            _clientCertificate = clientCertificate;
            return this;
        }

        public IAzureAppGatewayClientBuilder WithAzureCloud(string azureCloud)
        {
            if (string.IsNullOrWhiteSpace(azureCloud)) 
            {
                azureCloud = "public";
            }

            switch (azureCloud.ToLower())
            {
                case "china":
                    _azureCloudEndpoint = AzureAuthorityHosts.AzureChina;
                    break;
                case "germany":
                    _azureCloudEndpoint = AzureAuthorityHosts.AzureGermany;
                    break;
                case "government":
                    _azureCloudEndpoint = AzureAuthorityHosts.AzureGovernment;
                    break;
                default:
                    _azureCloudEndpoint = AzureAuthorityHosts.AzurePublicCloud;
                    break;
            }

            return this;
        }

        public IAzureAppGatewayClient Build()
        {
            ILogger logger = LogHandler.GetClassLogger<GatewayClient>();
            logger.LogDebug($"Creating client for Azure Resource Management with tenant ID '{_tenantId}' and application ID '{_applicationId}'.");

            // Setting up credentials for Azure Resource Management.
            DefaultAzureCredentialOptions credentialOptions = new DefaultAzureCredentialOptions
            {
                AuthorityHost = _azureCloudEndpoint,
                              AdditionallyAllowedTenants = { "*" } 
            };

            TokenCredential credential;
            if (!string.IsNullOrWhiteSpace(_clientSecret)) 
            {
                credential = new ClientSecretCredential(
                        _tenantId, _applicationId, _clientSecret, credentialOptions
                        );
            }
            else if (_clientCertificate != null) 
            {
                credential = new ClientCertificateCredential(
                        _tenantId, _applicationId, _clientCertificate, credentialOptions
                        );
            }
            else 
            {
                throw new Exception("Client secret or client certificate must be provided.");
            }

            // Creating Azure Resource Management client with the specified credentials.
            ArmClient armClient = new ArmClient(credential);
            _client._armClient = armClient;
            if (_resourceId != null) _client.AppGatewayResourceId = new ResourceIdentifier(_resourceId);
            _client._credential = credential;

            logger.LogTrace("Azure Resource Management client created.");
            return _client;
        }
    }

    private string RetrieveCertificateFromKeyVault(string vaultId)
    {
        _logger.LogDebug($"Retrieving certificate from Azure Key Vault with ID {vaultId}");

        SecretClientOptions options = new SecretClientOptions()
        {
            Retry =
            {
                Delay= TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromSeconds(16),
                MaxRetries = 5,
                Mode = RetryMode.Exponential
            }
        };

        // vaultId in the form of https://<vault-name>.vault.azure.net/secrets/<secret-name>[/<version>]
        Uri vaultUri = new Uri(vaultId);

        _logger.LogTrace($"Creating SecretClient object with URI {vaultUri.Scheme + "://" + vaultUri.Host}");
        SecretClient client = new SecretClient(new Uri(vaultUri.Scheme + "://" + vaultUri.Host), _credential, options);

        string secretName = null;
        string version = null;
        if (vaultUri.Segments.Length == 3)
        {
            secretName = vaultUri.Segments.Last().TrimEnd('/');
            _logger.LogTrace($"Retrieving secret called \"{secretName}\" from Azure Key Vault");
        }
        else if (vaultUri.Segments.Length == 4)
        {
            secretName = vaultUri.Segments[2].TrimEnd('/');
            version = vaultUri.Segments.Last().TrimEnd('/');
            _logger.LogTrace($"Retrieving secret called \"{secretName}\" with version \"{version}\" from Azure Key Vault");
        }
        else
        {
            throw new Exception($"Invalid Azure Key Vault secret ID: {vaultId}");
        }

        KeyVaultSecret secret = client.GetSecret(secretName, version);

        if (String.IsNullOrWhiteSpace(secret.Properties.ContentType) || secret.Properties.ContentType != "application/x-pkcs12")
        {
            throw new Exception($"Unexpected content type for secret {vaultUri.Segments.Last()}. Expected application/x-pkcs12, but got {secret.Properties.ContentType}");
        }
        
        string b64EncodedPkcs12Certificate = secret.Value;
        
        // Convert the PKCS#12 certificate to DER format
        X509Certificate2 pkcs12Certificate = new X509Certificate2(Convert.FromBase64String(b64EncodedPkcs12Certificate));
        string b64EncodedDerCertificate = Convert.ToBase64String(pkcs12Certificate.Export(X509ContentType.Cert));

        return b64EncodedDerCertificate;
    }

    private ApplicationGatewayCollection GetAppGatewayParentCollection()
    {
        _logger.LogTrace($"Getting parent collection for App Gateway with ID {AppGatewayResourceId}");

        // Use subscription resource to get resource group resource that contains App Gateway
        var subscriptionResource = _armClient.GetSubscriptionResource(new ResourceIdentifier(AppGatewayResourceId.Parent.Parent));

        ResourceGroupResource resourceGroupResource = subscriptionResource.GetResourceGroup(AppGatewayResourceId.ResourceGroupName);

        return resourceGroupResource.GetApplicationGateways();
    }

    public ApplicationGatewaySslCertificate AddCertificate(string name, string b64Pkcs12Certificate, string password)
    {
        ApplicationGatewayResource appGatewayResource = _armClient.GetApplicationGatewayResource(AppGatewayResourceId).Get();
        _logger.LogDebug($"Adding SSL certificate to App Gateway called \"{appGatewayResource.Data.Name}\"");

        // Create new certificate object with certificate data
        ApplicationGatewaySslCertificate gatewaySslCertificate = new ApplicationGatewaySslCertificate
        {
            Name = name,
                 Data = BinaryData.FromObjectAsJson(b64Pkcs12Certificate),
                 Password = password
        };

        appGatewayResource.Data.SslCertificates.Add(gatewaySslCertificate);
        ApplicationGatewayCollection parentCollection = GetAppGatewayParentCollection();

        appGatewayResource = parentCollection.CreateOrUpdate(
                WaitUntil.Completed,
                appGatewayResource.Data.Name,
                appGatewayResource.Data
                ).Value;

        _logger.LogDebug($"Added SSL certificate called \"{name}\" to App Gateway called \"{appGatewayResource.Data.Name}\"");

        ApplicationGatewaySslCertificate certificateObject = 
            appGatewayResource.Data.SslCertificates.FirstOrDefault(cert => cert.Name == name);

        return certificateObject;
    }

    public void RemoveCertificate(string certificateName)
    {
        ApplicationGatewayResource appGatewayResource =
            _armClient.GetApplicationGatewayResource(AppGatewayResourceId).Get();

        ApplicationGatewaySslCertificate gatewaySslCertificate =
            appGatewayResource.Data.SslCertificates.FirstOrDefault(c => c.Name == certificateName);
        if (gatewaySslCertificate == null)
        {
            _logger.LogDebug($"Certificate called \"{certificateName}\" not found in App Gateway called \"{appGatewayResource.Data.Name}\"");
            return;
        }

        // Don't remove the certificate if it is in use by a listener
        ApplicationGatewayHttpListener listener = appGatewayResource.Data.HttpListeners.FirstOrDefault(l => l.SslCertificateId == gatewaySslCertificate.Id);
        if (listener != null)
        {
            _logger.LogError($"Certificate called \"{certificateName}\" is in use by listener called \"{listener.Name}\" and cannot be removed.");

            throw new Exception($"Certificate called \"{certificateName}\" is in use by listener called \"{listener.Name}\" and cannot be removed.");
        }

        _logger.LogDebug($"Removing SSL certificate called \"{certificateName}\" from App Gateway called \"{appGatewayResource.Data.Name}\"");

        appGatewayResource.Data.SslCertificates.Remove(gatewaySslCertificate);

        GetAppGatewayParentCollection().CreateOrUpdate(
                WaitUntil.Completed,
                appGatewayResource.Data.Name,
                appGatewayResource.Data);

        _logger.LogDebug($"Successfully removed SSL certificate called \"{certificateName}\" from App Gateway called \"{appGatewayResource.Data.Name}\"");

        return;
    }

    public ApplicationGatewaySslCertificate GetAppGatewayCertificateByName(string certificateName)
    {
        _logger.LogDebug($"Getting SSL certificate called \"{certificateName}\" from App Gateway");

        ApplicationGatewayResource appGatewayResource =
            _armClient.GetApplicationGatewayResource(AppGatewayResourceId).Get();

        ApplicationGatewaySslCertificate gatewaySslCertificate =
            appGatewayResource.Data.SslCertificates.FirstOrDefault(c => c.Name == certificateName);

        return gatewaySslCertificate;
    }

    public OperationResult<IEnumerable<CurrentInventoryItem>> GetAppGatewaySslCertificates()
    {
        ApplicationGatewayResource appGatewayResource =
            _armClient.GetApplicationGatewayResource(AppGatewayResourceId).Get();
        _logger.LogDebug($"Getting SSL certificates from App Gateway called \"{appGatewayResource.Data.Name}\"");
        _logger.LogDebug($"There are {appGatewayResource.Data.SslCertificates.Count()} certificates in the response.");
        List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

        OperationResult<IEnumerable<CurrentInventoryItem>> result = new(inventoryItems);

        foreach (ApplicationGatewaySslCertificate certObject in appGatewayResource.Data.SslCertificates)
        {
            List<string> b64EncodedDerCertificateList = new List<string>();

            if (certObject.PublicCertData != null)
            {
                _logger.LogTrace($"Certificate called \"{certObject.Name}\" ({certObject.Id}) has public certificate data.");
                // ApplicationGatewaySslCertificate is in PKCS#7 format

                // Azure returns public cert data wrapped in parentheses. Remove them.
                byte[] untrimmedCertBytes = certObject.PublicCertData.ToArray();
                byte[] b64CertBytes = untrimmedCertBytes.Skip(1).Take(untrimmedCertBytes.Length - 2).ToArray();

                // Decode the PKCS#7 certificate to get individual certificates in DER format
                SignedCms pkcs7 = new SignedCms();
                pkcs7.Decode(Convert.FromBase64String(Encoding.UTF8.GetString(b64CertBytes)));

                // PKCS#7 can contain multiple certificates. Add each one to the list.
                foreach (X509Certificate2 cert in pkcs7.Certificates)
                {
                    b64EncodedDerCertificateList.Add(Convert.ToBase64String(cert.RawData));
                }
            }
            else if (certObject.PublicCertData == null && !string.IsNullOrEmpty(certObject.KeyVaultSecretId))
            {
                _logger.LogTrace($"Certificate called \"{certObject.Name}\" ({certObject.Id}) does not have any public certificate data, but has a Key Vault secret ID.");

                try
                {
                    string certificateFromKeyVault = RetrieveCertificateFromKeyVault(certObject.KeyVaultSecretId);
                    b64EncodedDerCertificateList.Add(certificateFromKeyVault);
                }
                catch (Exception e)
                {
                    string error = $"Failed to download certificate from Azure Key Vault with ID {certObject.KeyVaultSecretId}";
                    _logger.LogError(error + $": {e.Message}");

                    result.AddRuntimeErrorMessage(error);
                    continue;
                }
            }
            else 
            {
                string error = $"Certificate called \"{certObject.Name}\" ({certObject.Id}) does not have any public certificate data or Key Vault secret ID.";
                _logger.LogError(error);

                result.AddRuntimeErrorMessage(error);
                continue;
            }


            CurrentInventoryItem inventoryItem = new CurrentInventoryItem()
            {
                Alias = certObject.Name,
                      PrivateKeyEntry = true,
                      ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                      UseChainLevel = true,
                      Certificates = b64EncodedDerCertificateList
            };
            _logger.LogDebug($"Found certificate called \"{certObject.Name}\" ({certObject.Id})");
            inventoryItems.Add(inventoryItem);
        }

        if (!result.Success)
        {
            result.ErrorSummary = $"Application Gateway Certificate inventory may be incomplete. Successfully read {inventoryItems.Count()}/{appGatewayResource.Data.SslCertificates.Count()} certificates present in the Application Gateway called {AppGatewayResourceId.Name} ({AppGatewayResourceId})\nPlease see Orchestrator logs for more details. Error summary:";
        }

        _logger.LogDebug($"Found {inventoryItems.Count()} certificates in app gateway");
        return result;
    }

    public void UpdateHttpsListenerCertificate(ApplicationGatewaySslCertificate certificate, string listenerName)
    {
        ApplicationGatewayResource appGatewayResource = _armClient.GetApplicationGatewayResource(AppGatewayResourceId).Get();

        // First, verify that certificate exists in App Gateway
        if (!CertificateExists(certificate.Name))
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

        // Invalidate the cache of currently bound certificates
        currentlyBoundAppGatewayCertificateCache = null;

        _logger.LogDebug($"Successfully updated listener \"{listenerName}\" with certificate called \"{certificate.Name}\".");
    }

    public bool CertificateExists(string certificateName)
    {
        ApplicationGatewayResource appGatewayResource = _armClient.GetApplicationGatewayResource(AppGatewayResourceId).Get();
        return appGatewayResource.Data.SslCertificates.FirstOrDefault(c => c.Name == certificateName) != null;
    }

    public bool CertificateIsBoundToHttpsListener(string certificateName)
    {
        _logger.LogDebug($"Checking if certificate \"{certificateName}\" is bound to an HTTPS listener");

        ApplicationGatewayResource appGatewayResource = _armClient.GetApplicationGatewayResource(AppGatewayResourceId).Get();
        ApplicationGatewaySslCertificate certificate = appGatewayResource.Data.SslCertificates.FirstOrDefault(c => c.Name == certificateName);
        if (certificate == null) return false;
        ApplicationGatewayHttpListener listener = appGatewayResource.Data.HttpListeners.FirstOrDefault(l => l.SslCertificateId == certificate.Id);

        _logger.LogTrace($"Certificate \"{certificateName}\" is {(listener != null ? "" : "not ")}bound to an HTTPS listener");
        return listener != null;
    }

    // Returns a dictionary of listener names and their associated certificate names
    public IDictionary<string, string> GetBoundHttpsListenerCertificates()
    {
        if (currentlyBoundAppGatewayCertificateCache != null)
        {
            _logger.LogDebug("Returning cached Application Gateway SSL Certificates bound to HTTPS listeners");
            return currentlyBoundAppGatewayCertificateCache;
        }

        _logger.LogDebug($"Getting Application Gateway SSL Certificates bound to HTTPS listeners");

        ApplicationGatewayResource appGatewayResource = _armClient.GetApplicationGatewayResource(AppGatewayResourceId).Get();
        IDictionary<string, string> listenerCertificates = new Dictionary<string, string>();

        foreach (ApplicationGatewayHttpListener listener in appGatewayResource.Data.HttpListeners)
        {
            ApplicationGatewaySslCertificate certificate = appGatewayResource.Data.SslCertificates.FirstOrDefault(c => c.Id == listener.SslCertificateId);
            if (certificate != null)
            {
                _logger.LogTrace($"Listener \"{listener.Name}\" is bound to certificate \"{certificate.Name}\"");
                listenerCertificates.Add(listener.Name, certificate.Name);
            }
        }

        currentlyBoundAppGatewayCertificateCache = listenerCertificates;

        return listenerCertificates;
    }

    public IEnumerable<string> DiscoverApplicationGateways()
    {
        SubscriptionCollection subscriptionCollection = _armClient.GetSubscriptions();

        // Build a list of all App Gateway IDs in every subscription and resource group
        // that the service principal has access to
        List<string> appGatewayIds = new List<string>();

        foreach (SubscriptionResource subscription in subscriptionCollection)
        {
            _logger.LogDebug($"Searching for App Gateways in subscription \"{subscription.Data.DisplayName}\"");
            IEnumerable<string> appGateways = subscription.GetApplicationGateways().Select(appGateway => appGateway.Data.Id.ToString());

            _logger.LogTrace($"Found {appGateways.Count()} App Gateways in subscription \"{subscription.Data.DisplayName}\": {string.Join(", ", appGateways)}");

            appGatewayIds.AddRange(appGateways);
        }

        _logger.LogDebug($"Discovered {appGatewayIds .Count()} App Gateways");

        return appGatewayIds;
    }
}
