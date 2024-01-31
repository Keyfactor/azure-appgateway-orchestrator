
using System;
using Azure.ResourceManager.Network.Models;
using AzureApplicationGatewayOrchestratorExtension.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace AzureApplicationGatewayOrchestratorExtension.ListenerBindingJobs;

public class Management : IManagementJobExtension
{
    public IAzureAppGatewayClient Client { get; set; }
    public string ExtensionName => "AppGwBin";

    ILogger _logger = LogHandler.GetClassLogger<Management>();

    public JobResult ProcessJob(ManagementJobConfiguration config)
    {
        _logger.LogDebug("Beginning App Gateway Binding Management Job");

        if (Client == null)
        {
            Client = new AppGatewayJobClientBuilder<GatewayClient.Builder>()
                .WithCertificateStoreDetails(config.CertificateStoreDetails)
                .Build();
        }

        JobResult result = new JobResult
        {
            Result = OrchestratorJobStatusJobResult.Failure,
                   JobHistoryId = config.JobHistoryId
        };

        try
        {
            var operation = DetermineOperation(config);
            result.Result = operation switch
            {
                OperationType.Replace => ReplaceAndRebindCertificate(config),
                    OperationType.Add => AddAndBindCertificate(config),
                    _ => throw new Exception($"Invalid Management operation type [{config.OperationType}]")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing job: {ex.Message}");
            result.FailureMessage = ex.Message;
        }

        return result;
    }

    private OrchestratorJobStatusJobResult AddAndBindCertificate(ManagementJobConfiguration config)
    {
        _logger.LogDebug($"Beginning AddAndBindCertificate operation");

        // If a private key password was not provided, Command didn't return
        // the certificate in PKCS#12 format.
        if (string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword))
        {
            throw new Exception("Certificate must be in PKCS#12 format - no private key password provided.");
        }

        if (string.IsNullOrWhiteSpace(config.JobCertificate.Alias))
        {
            throw new Exception("Certificate alias is required.");
        }

        // Strategy:
        // The Certificate Alias from Keyfactor Command represents the name of an HTTPS listener
        // that is expected to be present on the App Gateway. If a certificate with the same
        // name is not present, we'll add it. If it is present, we'll just bind it to the listener.
        //
        // We don't care if the same certificate [content] is already present on the App Gateway
        // under a different alias since [as of 1/31/2024] the App Gateway API won't return an error,
        // and the type of operation that would rectify this situation could result in broken state
        // between Keyfactor Command and the App Gateway.

        ApplicationGatewaySslCertificate certificateToBind;

        if (!Client.CertificateExists(config.JobCertificate.Alias))
        {
            _logger.LogTrace($"Adding certificate with alias [{config.JobCertificate.Alias}]");

            certificateToBind = Client.AddCertificate(
                    config.JobCertificate.Alias,
                    config.JobCertificate.Contents,
                    config.JobCertificate.PrivateKeyPassword
                    );
        }
        else
        {
            _logger.LogTrace($"Certificate with alias [{config.JobCertificate.Alias}] already exists");

            certificateToBind = Client.GetAppGatewayCertificateByName(config.JobCertificate.Alias);
        }

        _logger.LogDebug($"Binding certificate with alias [{config.JobCertificate.Alias}] to listener [{config.JobCertificate.Alias}]");

        Client.UpdateHttpsListenerCertificate(certificateToBind, config.JobCertificate.Alias);

        _logger.LogDebug("AddAndBindCertificate operation complete");

        return OrchestratorJobStatusJobResult.Success;
    }

    private OrchestratorJobStatusJobResult ReplaceAndRebindCertificate(ManagementJobConfiguration config)
    {
        _logger.LogDebug("Beginning ReplaceAndRebindCertificate operation");

        // Strategy:
        // As with the Add operation, the Certificate Alias from Keyfactor Command represents the name
        // of the HTTPS listener that the user wants to bind the certificate to. The strategy will be to
        // 1. Determine the name of the certificate currently bound to the HTTPS listener called Alias
        // 2. Create and bind a temporary certificate to the HTTPS listener called Alias
        // 3. Delete the AppGatewayCertificate previously bound to the HTTPS listener called Alias
        // 4. Recreate and bind an AppGatewayCertificate with the same name as the previously bound certificate
        // 5. Delete the temporary certificate

        System.Collections.Generic.IDictionary<string, string> currentlyBoundAppGatewayCertificates = Client.GetBoundHttpsListenerCertificates();
        
        // Sanity check
        if (!currentlyBoundAppGatewayCertificates.ContainsKey(config.JobCertificate.Alias)) throw new Exception($"HTTPS Listener called [{config.JobCertificate.Alias}] does not exist on App Gateway"); 

        // Store the name of the certificate bound to the listener called Alias
        string originallyBoundCertificateName = currentlyBoundAppGatewayCertificates[config.JobCertificate.Alias];

        // Create and bind a temporary certificate to the HTTPS listener called Alias
        string tempAlias = Guid.NewGuid().ToString();
        _logger.LogTrace($"Creating temporary certificate called [{tempAlias}]");
        ApplicationGatewaySslCertificate temporaryAppGatewayCertificate = Client.AddCertificate(
                tempAlias,
                config.JobCertificate.Contents,
                config.JobCertificate.PrivateKeyPassword
                );

        _logger.LogTrace($"Binding temporary certificate with alias [{tempAlias}] to listener [{config.JobCertificate.Alias}]");
        Client.UpdateHttpsListenerCertificate(temporaryAppGatewayCertificate, config.JobCertificate.Alias);
        
        _logger.LogTrace($"Removing certificate called [{originallyBoundCertificateName}]");
        Client.RemoveCertificate(originallyBoundCertificateName);

        _logger.LogTrace($"Recreating certificate called [{originallyBoundCertificateName}]");
        ApplicationGatewaySslCertificate recreatedAppGatewayCertificate = Client.AddCertificate(
                config.JobCertificate.Alias,
                config.JobCertificate.Contents,
                config.JobCertificate.PrivateKeyPassword
                );

        _logger.LogTrace($"Binding recreated certificate called [{originallyBoundCertificateName}] to listener [{config.JobCertificate.Alias}]");
        Client.UpdateHttpsListenerCertificate(recreatedAppGatewayCertificate, config.JobCertificate.Alias);

        _logger.LogTrace($"Removing temporary certificate called [{tempAlias}]");
        Client.RemoveCertificate(tempAlias);

        return OrchestratorJobStatusJobResult.Success;
    }

    private enum OperationType
    {
        Add,
        Replace,
        None
    }

    private OperationType DetermineOperation(ManagementJobConfiguration config)
    {
        if (config.OperationType == CertStoreOperationType.Add && config.Overwrite)
            return OperationType.Replace;

        if (config.OperationType == CertStoreOperationType.Add)
            return OperationType.Add;

        // Remove not supported

        return OperationType.None;
    }

}
