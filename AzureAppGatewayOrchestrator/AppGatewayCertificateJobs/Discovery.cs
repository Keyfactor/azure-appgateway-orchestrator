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
using AzureApplicationGatewayOrchestratorExtension.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace AzureApplicationGatewayOrchestratorExtension.AppGatewayCertificateJobs;

public class Discovery : IDiscoveryJobExtension
{
    public IAzureAppGatewayClient Client { get; set; }
    public string ExtensionName => "AzureAppGw";

    private bool _clientInitializedByInjection = false;

    ILogger _logger = LogHandler.GetClassLogger<Discovery>();

    public JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate callback)
    {
        if (Client != null) _clientInitializedByInjection = true;

        _logger.LogDebug("Beginning App Gateway Discovery Job");

        JobResult result = new JobResult
        {
            Result = OrchestratorJobStatusJobResult.Failure,
                   JobHistoryId = config.JobHistoryId
        };

        List<string> discoveredAppGateways = new();

        foreach (var tenantId in TenantIdsToSearchFromJobConfig(config))
        {
            _logger.LogTrace($"Processing tenantId: {tenantId}");

            // If the client was not injected, create a new one with the tenant ID determied by
            // the TenantIdsToSearchFromJobConfig method
            if (!_clientInitializedByInjection)
            {
                Client = new AppGatewayJobClientBuilder<GatewayClient.Builder>()
                    .WithDiscoveryJobConfiguration(config, tenantId)
                    .Build();
            }

            try
            {
                discoveredAppGateways.AddRange(Client.DiscoverApplicationGateways());
            }catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing discovery job:\n {ex.Message}");
                result.FailureMessage = ex.Message;
                return result;
            }
        }

        try
        {
            callback(discoveredAppGateways);
            result.Result = OrchestratorJobStatusJobResult.Success;
        } catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing discovery job:\n {ex.Message}");
            result.FailureMessage = ex.Message;
        }

        return result;
    }

    private IEnumerable<string> TenantIdsToSearchFromJobConfig(DiscoveryJobConfiguration config)
    {
        string directoriesToSearchAsString = config.JobProperties?["dirs"] as string;
        _logger.LogTrace($"Directories to search: {directoriesToSearchAsString}");

        if (string.IsNullOrEmpty(directoriesToSearchAsString) || string.Equals(directoriesToSearchAsString, "*"))
        {
            _logger.LogTrace($"No directories to search provided, using default tenant ID: {config.ClientMachine}");
            return new List<string> { config.ClientMachine };
        }

        List<string> tenantIdsToSearch = new();
        tenantIdsToSearch.AddRange(directoriesToSearchAsString.Split(','));
        tenantIdsToSearch.ForEach(tenantId => tenantId = tenantId.Trim());

        _logger.LogTrace($"Tenant IDs to search: {string.Join(',', tenantIdsToSearch)}");
        return tenantIdsToSearch;
    }
}
