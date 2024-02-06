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

namespace AzureApplicationGatewayOrchestratorExtension.AppGatewayCertificateJobs;

public class Inventory : IInventoryJobExtension
{
    public IAzureAppGatewayClient Client { get; set; }
    public string ExtensionName => "AzureAppGW";

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
                   JobHistoryId = config.JobHistoryId
        };

        List<CurrentInventoryItem> inventoryItems;

        try
        {
            inventoryItems = Client.GetAppGatewaySslCertificates().ToList();
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting App Gateway SSL Certificates:\n" + ex.Message);
            result.FailureMessage = "Error getting App Gateway SSL Certificates:\n" + ex.Message;
            return result;
        }

        _logger.LogDebug($"Found {inventoryItems.Count} certificates in App Gateway");

        //cb.DynamicInvoke(inventoryItems);
        cb(inventoryItems);

        result.Result = OrchestratorJobStatusJobResult.Success;
        return result;
    }
}
