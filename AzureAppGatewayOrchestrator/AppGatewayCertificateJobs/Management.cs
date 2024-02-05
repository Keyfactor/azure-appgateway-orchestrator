
using System;
using AzureApplicationGatewayOrchestratorExtension.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace AzureApplicationGatewayOrchestratorExtension.AppGatewayCertificateJobs;

public class Management : IManagementJobExtension
{
    public IAzureAppGatewayClient Client { get; set; }
    public string ExtensionName => "AzureAppGW";

    ILogger _logger = LogHandler.GetClassLogger<Management>();

    public JobResult ProcessJob(ManagementJobConfiguration config)
    {
        _logger.LogDebug("Beginning App Gateway Management Job");

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
                OperationType.Replace => ReplaceCertificate(config),
                    OperationType.Add => AddCertificate(config),
                    OperationType.Remove => RemoveCertificate(config),
                    OperationType.DoNothing => OrchestratorJobStatusJobResult.Success,
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

    private enum OperationType
    {
        Add,
        Remove,
        Replace,
        DoNothing,
        None
    }

    private OperationType DetermineOperation(ManagementJobConfiguration config)
    {
        // If the operation is to add a gateway certificate and overwrite is true,
        // before executing a replace operation, check if the certificate is bound to
        // any listeners. If it is, DoNothing. If it isn't, Replace.
        //
        // This is because a Renew job was most likely executed on the AppGwBin certificate
        // store type, which will handle the replacement job.

        if (Client.CertificateIsBoundToHttpsListener(config.JobCertificate.Alias) && config.OperationType == CertStoreOperationType.Add && config.Overwrite)
        {
            _logger.LogDebug("Certificate is bound to an HTTPS listener; no action will be taken by AzureAppGW Management job.");
            return OperationType.DoNothing;
        }

        if (config.OperationType == CertStoreOperationType.Add && config.Overwrite)
            return OperationType.Replace;

        if (config.OperationType == CertStoreOperationType.Add)
            return OperationType.Add;

        if (config.OperationType == CertStoreOperationType.Remove)
            return OperationType.Remove;

        return OperationType.None;
    }

    private OrchestratorJobStatusJobResult AddCertificate(ManagementJobConfiguration config)
    {
        _logger.LogDebug("Beginning AddCertificate operation");

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

        _logger.LogTrace($"Adding certificate with alias [{config.JobCertificate.Alias}]");

        // Don't check if the certificate already exists; Command shouldn't allow non-unique
        // aliases to be added and if the certificate already exists, the operation should fail.

        Client.AddCertificate(
                config.JobCertificate.Alias,
                config.JobCertificate.Contents,
                config.JobCertificate.PrivateKeyPassword
                );

        _logger.LogDebug("AddCertificate operation complete");

        return OrchestratorJobStatusJobResult.Success;
    }

    private OrchestratorJobStatusJobResult ReplaceCertificate(ManagementJobConfiguration config)
    {
        _logger.LogDebug("Beginning ReplaceCertificate operation");

        RemoveCertificate(config);
        AddCertificate(config);

        _logger.LogDebug("ReplaceCertificate operation complete");

        return OrchestratorJobStatusJobResult.Success;
    }

    private OrchestratorJobStatusJobResult RemoveCertificate(ManagementJobConfiguration config)
    {
        _logger.LogDebug("Beginning RemoveCertificate operation");

        _logger.LogTrace($"Removing certificate with alias [{config.JobCertificate.Alias}]");

        // If the certificate doesn't exist, the operation should fail.

        Client.RemoveCertificate(config.JobCertificate.Alias);

        _logger.LogDebug("RemoveCertificate operation complete");

        return OrchestratorJobStatusJobResult.Success;
    }
}
