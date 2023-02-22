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
    [Job("Discovery")]
    public class Discovery : AzureAppGatewayJob<Discovery>, IDiscoveryJobExtension
    {
        ILogger _logger = LogHandler.GetClassLogger<Discovery>();

        public JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate callback)
        {
            _logger.LogDebug("Beginning App Gateway Discovery Job");
            
            JobResult result = new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = config.JobHistoryId
            };
            
            Initialize(config);

            try
            {
                callback(GatewayClient.DiscoverAppGateways());
                result.Result = OrchestratorJobStatusJobResult.Success;
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job:\n {0}", ex.Message);
                result.FailureMessage = ex.Message;
            }

            return result;
        }
    }
}