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
        public AzureAppGatewayClient(AzureProperties properties)
        {
            Log = LogHandler.GetClassLogger<AzureAppGatewayClient>();
            Log.LogDebug("Initializing Azure App Services client");

            // Construct Azure Resource Management client using ClientSecretCredential based on properties inside AzureProperties
            ArmClient = new ArmClient(new ClientSecretCredential(properties.TenantId, properties.ApplicationId, properties.ClientSecret));

            // Get subscription resource defined by resource ID
            Subscription = ArmClient.GetDefaultSubscription();
            Log.LogDebug("Found subscription called \"{SubscriptionDisplayName}\" ({SubscriptionId})",
                Subscription.Data.DisplayName, Subscription.Data.SubscriptionId);
        }
        
        private ILogger Log { get; }
        private ArmClient ArmClient { get; }
        private SubscriptionResource Subscription { get; }

        public ResourceIdentifier AppGatewayResourceId
        {
            set
            {
                ApplicationGatewayResource appGatewayResource = ArmClient.GetApplicationGatewayResource(value).Get();
                Log.LogDebug("Found App Gateway called \"{AppGatewayName}\" ({AppGatewayId})",
                    appGatewayResource.Data.Name, appGatewayResource.Id);
                _appGatewayResourceId = value;
            }
        }
        
        private ResourceIdentifier _appGatewayResourceId;

        private ApplicationGatewayCollection GetAppGatewayCollection()
        {
            // Use subscription resource to get resource group resource that contains App Gateway
            ResourceGroupResource resourceGroupResource = Subscription.GetResourceGroup(_appGatewayResourceId.ResourceGroupName);
            return resourceGroupResource.GetApplicationGateways();
        }
        
        public IEnumerable<CurrentInventoryItem> GetAppGatewaySslCertificates()
        {
            ApplicationGatewayResource appGatewayResource =
                ArmClient.GetApplicationGatewayResource(_appGatewayResourceId).Get();
            Log.LogDebug("Getting SSL certificates from App Gateway called \"{AppGatewayName}\"", appGatewayResource.Data.Name);
            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();
            
            foreach (ApplicationGatewaySslCertificate certObject in appGatewayResource.Data.SslCertificates)
            {
                // ApplicationGatewaySslCertificate is in PKCS#7 format

                // Azure returns public cert data wrapped in parentheses. Remove them.
                byte[] untrimmedCertBytes = certObject.PublicCertData.ToArray();
                byte[] b64CertBytes = untrimmedCertBytes.Skip(1).Take( untrimmedCertBytes.Length - 2 ).ToArray();
                
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
                inventoryItems.Add(inventoryItem);
            }
            Log.LogDebug("Found {CertificateCount} certificates in app gateway", inventoryItems.Count);
            return inventoryItems;
        }
        
        public ApplicationGatewaySslCertificate AddAppGatewaySslCertificate(string certificateName, string certificateData, string certificatePassword)
        {
            ApplicationGatewayResource appGatewayResource =
                ArmClient.GetApplicationGatewayResource(_appGatewayResourceId).Get();
            Log.LogDebug("Adding SSL certificate to App Gateway called \"{AppGatewayName}\"", appGatewayResource.Data.Name);
            
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
            appGatewayResource = GetAppGatewayCollection().CreateOrUpdate(WaitUntil.Completed, appGatewayResource.Data.Name, appGatewayResource.Data).WaitForCompletion();
            Log.LogDebug("Added SSL certificate called \"{CertificateName}\" to App Gateway called \"{AppGatewayName}\"", certificateName, appGatewayResource.Data.Name);
            
            return appGatewayResource.Data.SslCertificates.FirstOrDefault(cert => cert.Name == certificateName);
        }

        public void RemoveAppGatewaySslCertificate(string certificateName)
        {
            ApplicationGatewayResource appGatewayResource =
                ArmClient.GetApplicationGatewayResource(_appGatewayResourceId).Get();
            Log.LogDebug("Removing SSL certificate called \"{CertificateName}\" from App Gateway called \"{AppGatewayName}\"", certificateName, appGatewayResource.Data.Name);
            
            // Find the certificate object called certificateName
            ApplicationGatewaySslCertificate gatewaySslCertificate = appGatewayResource.Data.SslCertificates.FirstOrDefault(c => c.Name == certificateName);
            if (gatewaySslCertificate == null)
            {
                Log.LogDebug("Certificate called \"{CertificateName}\" not found in App Gateway called \"{AppGatewayName}\"", certificateName, appGatewayResource.Data.Name);
                return;
            }
            
            // Determine if the certificate is in use by any listeners
            ApplicationGatewayHttpListener listener = appGatewayResource.Data.HttpListeners.FirstOrDefault(l => l.SslCertificateId == gatewaySslCertificate.Id);
            if (listener != null)
            {
                // If a listener is using the certificate, reassign it the first certificate found that isn't the one we're removing
                ApplicationGatewaySslCertificate newCertificate = appGatewayResource.Data.SslCertificates.FirstOrDefault(c => c.Name != certificateName);
                if (newCertificate == null)
                {
                    string error = $"Certificate called \"{certificateName}\" is in use by listener \"{listener.Name}\" and no other certificates are available for reassignment.";
                    Log.LogError(error);
                    throw new Exception(error);
                }
                // Reassign the listener to use the new certificate
                Log.LogDebug("Certificate called \"{CertificateName}\" is in use by listener \"{ListenerName}\".", certificateName, listener.Name);
                UpdateAppGatewayListenerCertificate(newCertificate, listener.Name);
                
                // If update succeeded, appGatewayResource is out of date. Get it again.
                appGatewayResource = ArmClient.GetApplicationGatewayResource(_appGatewayResourceId).Get();
            }

            // Remove the certificate object from the App Gateway resource
            appGatewayResource.Data.SslCertificates.Remove(gatewaySslCertificate);
            
            // Update the App Gateway resource
            GetAppGatewayCollection().CreateOrUpdate(WaitUntil.Completed, appGatewayResource.Data.Name, appGatewayResource.Data);
            
            Log.LogDebug("Successfully removed SSL certificate called \"{CertificateName}\" from App Gateway called \"{AppGatewayName}\"", certificateName, appGatewayResource.Data.Name);
        }

        public void UpdateAppGatewayListenerCertificate(ApplicationGatewaySslCertificate certificate, string listenerName)
        {
            ApplicationGatewayResource appGatewayResource = ArmClient.GetApplicationGatewayResource(_appGatewayResourceId).Get();
            
            // First, verify that certificate exists in App Gateway
            if (appGatewayResource.Data.SslCertificates.FirstOrDefault(c => c.Name == certificate.Name) == null)
            {
                string error =
                    $"Certificate with name \"{certificate.Name}\" does not exist in App Gateway \"{appGatewayResource.Data.Name}\"";
                Log.LogError(error);
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
                Log.LogError(error);
                throw new Exception(error);
            }
            Log.LogDebug("Updating listener \"{ListenerName}\" to use certificate \"{CertificateName}\"", listenerName, certificate.Name);
            
            // Update the App Gateway resource
            appGatewayResource.Data.HttpListeners.Remove(appGatewayResource.Data.HttpListeners.FirstOrDefault(l => l.Name == listenerName));
            appGatewayResource.Data.HttpListeners.Add(listener);
            
            GetAppGatewayCollection().CreateOrUpdate(WaitUntil.Completed, appGatewayResource.Data.Name, appGatewayResource.Data);
        }
        
        public IEnumerable<string> DiscoverAppGateways()
        {
            // Build a list of all App Gateway IDs in every resource group
            List<string> appGatewayIds = new List<string>();
            foreach (ResourceGroupResource rg in Subscription.GetResourceGroups().GetAll())
            {
                appGatewayIds.AddRange(rg.GetApplicationGateways().GetAll().Select(appGateway => appGateway.Data.Id.ToString()));
            }

            return appGatewayIds;
        }
    }
}
