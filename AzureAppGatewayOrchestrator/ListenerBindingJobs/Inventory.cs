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
using AzureApplicationGatewayOrchestratorExtension.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace AzureApplicationGatewayOrchestratorExtension.ListenerBindingJobs;

public class Inventory : IInventoryJobExtension
{
    public string ExtensionName => "AzureAppGwBin";
    public IAzureAppGatewayClient Client { get; set; }

    ILogger _logger = LogHandler.GetClassLogger<Inventory>();

    public JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate cb)
    {
        _logger.LogDebug($"Beginning App Gateway Inventory Job");

        if (Client == null)
        {
            Client = new AppGatewayJobClientBuilder<GatewayClient.Builder>()
                .WithCertificateStoreDetails(config.CertificateStoreDetails)
                .Build();
        }

        JobResult result = new JobResult
        {
            Result = OrchestratorJobStatusJobResult.Failure,
                   JobHistoryId = config.JobHistoryId,
                   FailureMessage = ""
        };

        List<CurrentInventoryItem> appGatewayCertificateInventory;

        // The AppGwBin Inventory job returns certificates from the configured Azure
        // Application Gateway that *are bound* to an HTTPS listener.
        //
        // The Certificate Alias reported back to Keyfactor Command is the name of the 
        // HTTPS listener that the certificate is bound to.
        //
        // Strategy:
        // First get all certificates present in the App Gateway, then filter out the ones
        // that are not bound to an HTTPS listener. This way the number of API calls
        // to the App Gateway is minimized.

        try
        {
            OperationResult<IEnumerable<CurrentInventoryItem>> inventoryResult = Client.GetAppGatewaySslCertificates();
            if (!inventoryResult.Success)
            {
                // Aggregate the messages into the failure message. Since an exception wasn't thrown,
                // we still have a partial success. We want to return a warning.
                result.FailureMessage += inventoryResult.ErrorMessage; 
                result.Result = OrchestratorJobStatusJobResult.Warning;
                _logger.LogWarning(result.FailureMessage);
            } 
            else
            {
                result.Result = OrchestratorJobStatusJobResult.Success;
            }

            // At least partial success is guaranteed, so we can continue with the inventory items
            // that we were able to pull down.
            appGatewayCertificateInventory = inventoryResult.Result.ToList();

        } catch (Exception ex)
        {
            // Exception is triggered if we weren't able to pull down the list of certificates
            // from Azure. This could be due to a number of reasons, including network issues,
            // or the user not having the correct permissions. An exception won't be triggered
            // if there are no certificates in the App Gateway, or if we weren't able to assemble
            // the list of certificates into a CurrentInventoryItem.

            _logger.LogError(ex, "Error getting App Gateway SSL Certificates:\n" + ex.Message);
            result.FailureMessage = "Error getting App Gateway SSL Certificates:\n" + ex.Message;
            return result;
        }

        _logger.LogDebug($"Found {appGatewayCertificateInventory.Count} certificates in App Gateway");

        // Get all HTTPS listeners in the App Gateway and the name of the certificate bound to them
        // This dict in the form of <HTTPS Listener Name, Certificate Name/Alias>
        IDictionary<string, string> httpsListenerCertificateBinding;
        try
        {
            httpsListenerCertificateBinding = Client.GetBoundHttpsListenerCertificates();
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bound App Gateway HTTPS Listener Certificates:\n" + ex.Message);
            result.FailureMessage += "Error getting bound App Gateway HTTPS Listener Certificates:\n" + ex.Message;
            return result;
        }

        _logger.LogDebug($"There are {httpsListenerCertificateBinding.Count} HTTPS listeners in App Gateway with bound certificates");

        // Sacrifice spacial complexity for time complexity - this way the loop that constructs the final
        // inventory list is O(n) instead of O(n^2)
        Dictionary<string, CurrentInventoryItem> appGatewayCertificateInventoryDict = appGatewayCertificateInventory.ToDictionary(x => x.Alias);

        List<CurrentInventoryItem> certificateBindingInventory = new List<CurrentInventoryItem>();
        foreach (KeyValuePair<string, string> listenerBinding in httpsListenerCertificateBinding)
        {
            // It's not guaranteed that the name of the HTTPS listener is the same as the certificate name/alias.
            // Additionally, the same certificate can be bound to multiple HTTPS listeners.

            // Determine if there is a certificate in the App Gateway Certificate inventory
            // with the same name as the certificate (listenerBinding.Value) bound to the 
            // HTTPS listener (listenerBinding.Key)
            if (appGatewayCertificateInventoryDict.ContainsKey(listenerBinding.Value))
            {
                // Update the inventory item with the name of the certificate bound to the HTTPS listener
                // to be the name of the HTTPS listener
                
                // We need to make a deep copy of the original inventory item since the same certificate can be 
                // bound to multiple HTTPS listeners

                CurrentInventoryItem newItem = new()
                {
                    Alias = listenerBinding.Key,
                    PrivateKeyEntry = appGatewayCertificateInventoryDict[listenerBinding.Value].PrivateKeyEntry,
                    ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                    UseChainLevel = false,
                    Certificates = appGatewayCertificateInventoryDict[listenerBinding.Value].Certificates,
                    Parameters = appGatewayCertificateInventoryDict[listenerBinding.Value].Parameters
                };
                
                certificateBindingInventory.Add(newItem);

                _logger.LogTrace($"Added certificate [{listenerBinding.Value}] bound to HTTPS listener [{listenerBinding.Key}] to inventory");
            }
        }

        _logger.LogDebug($"Found {certificateBindingInventory.Count} certificates bound to HTTPS listeners in App Gateway");
        _logger.LogTrace($"Of the {appGatewayCertificateInventory.Count} certificates in App Gateway, there are {certificateBindingInventory.Count} bound to HTTPS listeners (possibly the same certificate bound to multiple listeners)");

        cb.DynamicInvoke(certificateBindingInventory);

        // Result is already set correctly by this point.
        return result;
    }
}
