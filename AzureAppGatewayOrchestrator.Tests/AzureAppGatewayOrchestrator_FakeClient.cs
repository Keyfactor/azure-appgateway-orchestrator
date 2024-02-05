

using Azure.ResourceManager.Network.Models;
using AzureApplicationGatewayOrchestratorExtension.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace AzureAppGatewayOrchestrator.Tests;

public class FakeClient : IAzureAppGatewayClient
{
        public class FakeBuilder : IAzureAppGatewayClientBuilder
        {
            private FakeClient _client = new FakeClient();

            public string _tenantId { get; set; }
            public string _resourceId { get; set; }
            public string _applicationId { get; set; }
            public string _clientSecret { get; set; }
            public string _azureCloudEndpoint { get; set; }

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

            public IAzureAppGatewayClientBuilder WithAzureCloud(string azureCloud)
            {
                _azureCloudEndpoint = azureCloud;
                return this;
            }

            public IAzureAppGatewayClient Build()
            {
                return _client;
            }
        }

    ILogger _logger = LogHandler.GetClassLogger<FakeClient>();

    public IEnumerable<string>? AppGatewaysAvailableOnFakeTenant { get; set; }
    public Dictionary<string, ApplicationGatewaySslCertificate>? CertificatesAvailableOnFakeAppGateway { get; set; }

    public IEnumerable<CurrentInventoryItem> GetAppGatewaySslCertificates()
    {
        _logger.LogDebug("Getting App Gateway SSL Certificates from fake app gateway");

        if (CertificatesAvailableOnFakeAppGateway == null)
        {
            throw new Exception("Get App Gateway SSL Certificate method failure - no inventory items set");
        }

        List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();
        foreach (ApplicationGatewaySslCertificate cert in CertificatesAvailableOnFakeAppGateway.Values)
        {
            inventoryItems.Add(new CurrentInventoryItem
            {
                Alias = cert.Name,
                PrivateKeyEntry = false,
                ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                UseChainLevel = true,
                Certificates = new List<string> { cert.Name }
            });
        }

        _logger.LogDebug($"Fake client has {inventoryItems.Count} certificates in inventory");

        return inventoryItems;
    }

    public ApplicationGatewaySslCertificate AddCertificate(string certificateName, string certificateData, string certificatePassword)
    {
        _logger.LogDebug($"Adding certificate {certificateName} to fake app gateway");

        if (CertificatesAvailableOnFakeAppGateway == null)
        {
            CertificatesAvailableOnFakeAppGateway = new Dictionary<string, ApplicationGatewaySslCertificate>();
        }

        ApplicationGatewaySslCertificate cert = new ApplicationGatewaySslCertificate
        {
            Name = certificateName,
            Data = BinaryData.FromObjectAsJson(certificateData),
            // Reserve the Password field for tracking certificates bound to HTTPS listeners
            Password = ""
        };

        _logger.LogDebug($"Adding certificate {certificateName} to fake app gateway");

        CertificatesAvailableOnFakeAppGateway.Add(certificateName, cert);

        _logger.LogTrace($"Fake client has {CertificatesAvailableOnFakeAppGateway.Count} certificates in inventory");

        return cert;
    }

    public void RemoveCertificate(string certificateName)
    {
        if (CertificatesAvailableOnFakeAppGateway == null || !CertificatesAvailableOnFakeAppGateway.ContainsKey(certificateName))
        {
            throw new Exception("Certificate not found");
        }

        // FakeClient tracks certificates bound to HTTPS listeners by 
        // the Password field of the ApplicationGatewaySslCertificate
        if (!string.IsNullOrWhiteSpace(CertificatesAvailableOnFakeAppGateway[certificateName].Password))
        {
            throw new Exception("Certificate is bound to an HTTPS listener");
        }

        _logger.LogDebug($"Removing certificate {certificateName} from fake app gateway");

        CertificatesAvailableOnFakeAppGateway.Remove(certificateName);

        _logger.LogTrace($"Fake client has {CertificatesAvailableOnFakeAppGateway.Count} certificates in inventory");
        return;
    }

    public bool CertificateExists(string certificateName)
    {
        if (CertificatesAvailableOnFakeAppGateway != null)
        {
            return CertificatesAvailableOnFakeAppGateway.ContainsKey(certificateName);
        }

        return false;
    }

    public ApplicationGatewaySslCertificate GetAppGatewayCertificateByName(string certificateName)
    {
        if (CertificatesAvailableOnFakeAppGateway == null || !CertificatesAvailableOnFakeAppGateway.ContainsKey(certificateName))
        {
            throw new Exception("Certificate not found");
        }

        _logger.LogDebug($"Getting certificate {certificateName} from fake app gateway");

        return CertificatesAvailableOnFakeAppGateway[certificateName];
    }

    public IEnumerable<string> DiscoverApplicationGateways()
    {
        if (AppGatewaysAvailableOnFakeTenant == null)
        {
            throw new Exception("Discover Application Gateways method failure - no app gateways set");
        }

        return AppGatewaysAvailableOnFakeTenant;
    }

    public void UpdateHttpsListenerCertificate(ApplicationGatewaySslCertificate certificate, string listenerName)
    {
        if (CertificatesAvailableOnFakeAppGateway == null || !CertificatesAvailableOnFakeAppGateway.ContainsKey(certificate.Name))
        {
            throw new Exception("Certificate not found");
        }

        _logger.LogDebug($"Binding certificate {certificate.Name} to listener {listenerName}");

        // In Azure, only one certificate can be bound to an HTTPS listener at a time.
        // If a certificate binding already exists, remove it before binding the new certificate.
        foreach (var cert in CertificatesAvailableOnFakeAppGateway.Values)
        {
            if (cert.Password == listenerName)
            {
                cert.Password = "";
            }
        }

        CertificatesAvailableOnFakeAppGateway[certificate.Name].Password = listenerName;
        return;
    }

    public bool CertificateIsBoundToHttpsListener(string certificateName)
    {
        if (CertificatesAvailableOnFakeAppGateway == null || !CertificatesAvailableOnFakeAppGateway.ContainsKey(certificateName))
        {
            return false; 
        }

        return !string.IsNullOrWhiteSpace(CertificatesAvailableOnFakeAppGateway[certificateName].Password);
    }

    public IDictionary<string, string> GetBoundHttpsListenerCertificates()
    {
        Dictionary<string, string> listenerCertificates = new Dictionary<string, string>();

        if (CertificatesAvailableOnFakeAppGateway == null)
        {
            return listenerCertificates;
        }

        foreach (var cert in CertificatesAvailableOnFakeAppGateway.Values)
        {
            if (!string.IsNullOrWhiteSpace(cert.Password))
            {
                listenerCertificates.Add(cert.Password, cert.Name);
                _logger.LogTrace($"Fake client has certificate {cert.Name} bound to listener {cert.Password}");
            }
        }

        _logger.LogDebug($"Fake client has {listenerCertificates.Count} certificates bound to HTTPS listeners");

        return listenerCertificates;
    }
}
