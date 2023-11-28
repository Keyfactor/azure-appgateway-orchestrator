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
            
            if (GatewayClient.AppGatewaySslCertificateExists(config.JobCertificate.Alias) && !config.Overwrite)
            {
                string message =
                    $"Certificate with alias \"{config.JobCertificate.Alias}\" already exists in App Gateway, and job was not configured to overwrite.";
                _logger.LogDebug(message);
                throw new Exception(message);
            }
            
            string listenerName = config.JobProperties["HTTPListenerName"]?.ToString();
            if (!string.IsNullOrWhiteSpace(config.JobProperties["HTTPListenerName"]?.ToString()))
            {
                _logger.LogDebug("Enrollment field 'HTTPListenerName' is set to \"{0}\". Also updating HTTP Listener.", config.JobProperties["HTTPListenerName"].ToString());
            }
            
            if (config.Overwrite)
            {
                _logger.LogDebug("Overwrite is enabled, replacing certificate in gateway called \"{0}\"", config.JobCertificate.Alias);
                GatewayClient.ReplaceAppGatewayCertificate(config.JobCertificate.Alias, config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword);
            }
            else
            {
                _logger.LogDebug("Adding certificate to App Gateway");
                GatewayClient.AddAppGatewaySslCertificate(config.JobCertificate.Alias, config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword, listenerName);
            }
        }
    }
}