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

        // There are two goals for this operation:
        // 1. Replace the certificate currently bound to the HTTPS listener called Alias
        // 2. Shift the state of the App Gateway in Azure to reflect the App Gateway Orchestrator Extension's
        //    expectation of how certificates are named. IE, the certificate called Alias should be bound to the
        //    listener called Alias.
        //
        // Strategy:
        // As with the Add operation, the Certificate Alias from Keyfactor Command represents the name
        // of the HTTPS listener that the user wants to bind the certificate to. The strategy will be to
        // 1. Determine the name of the certificate currently bound to the HTTPS listener - Alias in 100% of cases
        //    if the certificate was originally added by the App Gateway Orchestrator Extension, or something else
        //    if the certificate was added by some other means (IE, the Azure Portal, or some other API client).
        // 2. Create and bind a temporary certificate to the HTTPS listener called Alias
        // 3. Delete the AppGatewayCertificate previously bound to the HTTPS listener called Alias
        // 4. Recreate and bind an AppGatewayCertificate with the same name as the HTTPS listener called Alias
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

        _logger.LogTrace($"Recreating certificate previously called [{originallyBoundCertificateName}] with alias [{config.JobCertificate.Alias}]");
        ApplicationGatewaySslCertificate recreatedAppGatewayCertificate = Client.AddCertificate(
                config.JobCertificate.Alias,
                config.JobCertificate.Contents,
                config.JobCertificate.PrivateKeyPassword
                );

        _logger.LogTrace($"Binding recreated certificate called [{config.JobCertificate.Alias}] to listener [{config.JobCertificate.Alias}]");
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
        if (config.OperationType != CertStoreOperationType.Add) return OperationType.None;

        // Scenarios:
        // 1. In general, this certificate store type expects the name of the certificate to be the same as the name
        //    of the HTTPS listener that its bound to. If the certificate bound to the listener is different than the
        //    certificate being added, we want to add the certificate and bind it to the listener. This case is 
        //    observable when the following conditions are met:
        //    - OperationType is Add
        //    - Overwrite is either true or false, it doesn't matter
        //    - The certificate currently bound to the listener (Alias) is different than the name of the Alias
        //
        // 2. If the certificate called Alias already exists and is bound to the listener called Alias, we
        //    want to replace it no matter what. This case is observable when the following conditions are met:
        //    - OperationType is Add
        //    - Overwrite is either true or false, it doesn't matter
        //    - The certificate currently bound to the listener called Alias is the same as the name of the Alias
        //
        // 3. If the certificate was imported from Azure Key Vault and is not bound to any listener, we'll apply the
        //    exact same logic as case 1 or 2. In every case, this will break the link between the certificate/secret
        //    in Azure Key Vault and the App Gateway. This is an acceptable risk because the App Gateway Orchestrator
        //    Extension is the only client that should be managing the App Gateway's certificates.

        string currentlyBoundCertificateName;
        System.Collections.Generic.IDictionary<string, string> currentlyBoundAppGatewayCertificates = Client.GetBoundHttpsListenerCertificates();

        // Sanity check
        if (!currentlyBoundAppGatewayCertificates.ContainsKey(config.JobCertificate.Alias)) throw new Exception($"HTTPS Listener called [{config.JobCertificate.Alias}] does not exist on App Gateway");

        currentlyBoundCertificateName = currentlyBoundAppGatewayCertificates[config.JobCertificate.Alias];
        if (currentlyBoundCertificateName == config.JobCertificate.Alias)
        {
            return OperationType.Replace;
        }
        else
        {
            return OperationType.Add;
        }
    }
}
