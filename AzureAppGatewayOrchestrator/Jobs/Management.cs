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
                        _logger.LogDebug("Got Add operation");
                        ApplicationGatewaySslCertificate cert = PerformAddition(config.JobCertificate);

                        if (!string.IsNullOrWhiteSpace(config.JobProperties["HTTPListenerName"]?.ToString()))
                        {
                            try
                            {
                                GatewayClient.UpdateAppGatewayListenerCertificate(cert, config.JobProperties["HTTPListenerName"].ToString());
                            } catch (Exception)
                            {
                                // If we fail to update the listener, we want to remove the certificate from the gateway.
                                // Otherwise, we'll have a certificate in the gateway that isn't being used and existing
                                // in a limbo state in Keyfactor.
                                GatewayClient.RemoveAppGatewaySslCertificate(config.JobCertificate.Alias);
                                throw;
                            }
                        }
                        
                        result.Result = OrchestratorJobStatusJobResult.Success;
                        break;
                    case CertStoreOperationType.Remove:
                        _logger.LogDebug("Got Remove operation");
                        GatewayClient.RemoveAppGatewaySslCertificate(config.JobCertificate.Alias);
                        result.Result = OrchestratorJobStatusJobResult.Success;
                        break;
                }
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job:\n {0}", ex.Message);
                result.FailureMessage = ex.Message;
            }

            return result;
        }

        private ApplicationGatewaySslCertificate PerformAddition(ManagementJobCertificate certificate)
        {
            if (string.IsNullOrWhiteSpace(certificate.PrivateKeyPassword))
            {
                throw new Exception("Certificate must be in PKCS#12 format.");
            }

            if (string.IsNullOrWhiteSpace(certificate.Alias))
            {
                throw new Exception("Certificate alias is required.");
            }
            
            return GatewayClient.AddAppGatewaySslCertificate(certificate.Alias, certificate.Contents, certificate.PrivateKeyPassword);
        }
    }
}