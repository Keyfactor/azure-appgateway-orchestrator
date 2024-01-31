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

using AzureApplicationGatewayOrchestratorExtension.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace AzureApplicationGatewayOrchestratorExtension.ListenerBindingJobs;

public class Discovery : IDiscoveryJobExtension
{
    public string ExtensionName => "AppGwBin";
    public IAzureAppGatewayClient Client { get; set; }

    ILogger _logger = LogHandler.GetClassLogger<Discovery>();

    public JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate callback)
    {
        _logger.LogDebug("Beginning App Gateway Listener Binding Discovery Job - using AppGatewayCertificateJobs.Discovery.ProcessJob()");

        AppGatewayCertificateJobs.Discovery discovery = new AppGatewayCertificateJobs.Discovery
        {
            Client = Client
        };

        return discovery.ProcessJob(config, callback);
    }
}
