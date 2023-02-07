using System;
using Azure.ResourceManager.Network.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.AzureAppGateway.Jobs
{
    public class Management : AzureAppGatewayJob<Management>, IManagementJobExtension
    {
        ILogger _logger = LogHandler.GetClassLogger<Management>();
        
        public JobResult ProcessJob(ManagementJobConfiguration config)
        {
            _logger.LogDebug("Beginning App Gateway Management Job");
            
            Initialize(config.CertificateStoreDetails);
            
            JobResult result = new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = config.JobHistoryId
            };

            try
            {
                switch (config.OperationType)
                {
                    case CertStoreOperationType.Add:
                        _logger.LogDebug("Adding certificate to App Gateway");
                        
                        PerformAddition(config);
                        
                        _logger.LogDebug("Add operation complete.");
                        
                        result.Result = OrchestratorJobStatusJobResult.Success;
                        break;
                    case CertStoreOperationType.Remove:
                        _logger.LogDebug("Removing certificate from App Gateway");
                        
                        GatewayClient.RemoveAppGatewaySslCertificate(config.JobCertificate.Alias);
                        
                        _logger.LogDebug("Remove operation complete.");
                        result.Result = OrchestratorJobStatusJobResult.Success;
                        break;
                    default:
                        _logger.LogDebug("Invalid management operation type: {0}", config.OperationType);
                        throw new ArgumentOutOfRangeException();
                }
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job:\n {0}", ex.Message);
                result.FailureMessage = ex.Message;
            }

            return result;
        }

        private void PerformAddition(ManagementJobConfiguration config)
        {
            // Ensure that the certificate is in PKCS#12 format.
            if (string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword))
            {
                throw new Exception("Certificate must be in PKCS#12 format.");
            }
            // Ensure that an alias is provided.
            if (string.IsNullOrWhiteSpace(config.JobCertificate.Alias))
            {
                throw new Exception("Certificate alias is required.");
            }
            
            ApplicationGatewaySslCertificate cert =  GatewayClient.AddAppGatewaySslCertificate(config.JobCertificate.Alias, config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword);
            
            _logger.LogDebug("Added certificate to App Gateway called \"{0}\" ({1})", cert.Id, cert.Name);

            if (string.IsNullOrWhiteSpace(config.JobProperties["HTTPListenerName"]?.ToString())) return;
            
            _logger.LogDebug("Enrollment field 'HTTPListenerName' is set to \"{0}\". Updating listener with new certificate.", config.JobProperties["HTTPListenerName"].ToString());
            try
            {
                GatewayClient.UpdateAppGatewayListenerCertificate(cert, config.JobProperties["HTTPListenerName"].ToString());
            } catch (Exception)
            {
                // If we fail to update the listener, we want to remove the certificate from the gateway.
                // Otherwise, we'll have a certificate in the gateway that isn't being used and existing
                // in a limbo state in Keyfactor.
                _logger.LogWarning("Failed to update listener with new certificate. Removing certificate from App Gateway.");
                GatewayClient.RemoveAppGatewaySslCertificate(config.JobCertificate.Alias);
                throw;
            }
                
            _logger.LogDebug("Updated listener with new certificate.");
        }
    }
}