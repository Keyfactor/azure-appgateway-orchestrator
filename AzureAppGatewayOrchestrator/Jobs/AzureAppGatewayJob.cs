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

using System.Runtime.CompilerServices;
using Azure.Core;
using Keyfactor.Extensions.Orchestrator.AzureAppGateway.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.AzureAppGateway.Jobs
{
    public abstract class AzureAppGatewayJob<T> : IOrchestratorJobExtension
    {
        public string ExtensionName => "AzureAppGW";

        protected AzureAppGatewayClient GatewayClient { get; private set; }

        protected void Initialize(CertificateStore details)
        {
            ILogger logger = LogHandler.GetReflectedClassLogger(this);
            logger.LogDebug($"Certificate Store Configuration: {JsonConvert.SerializeObject(details)}");
            logger.LogDebug("Initializing AzureAppGatewayClient");
            dynamic properties = JsonConvert.DeserializeObject(details.Properties);
            
            AzureProperties azureProperties = new AzureProperties
            {
                TenantId = details.ClientMachine,
                ApplicationId = properties?.ServerUsername,
                ClientSecret = properties?.ServerPassword
            };
            
            GatewayClient = new AzureAppGatewayClient(azureProperties)
            {
                AppGatewayResourceId = new ResourceIdentifier(details.StorePath)
            };
        }

        protected void Initialize(DiscoveryJobConfiguration config)
        {
            ILogger logger = LogHandler.GetReflectedClassLogger(this);
            logger.LogDebug($"Discovery Job Configuration: {JsonConvert.SerializeObject(config)}");
            logger.LogDebug("Initializing AzureAppGatewayClient");
            AzureProperties azureProperties = new AzureProperties
            {
                TenantId = config.ClientMachine,
                ApplicationId = config.ServerUsername,
                ClientSecret = config.ServerPassword
            };
            
            GatewayClient = new AzureAppGatewayClient(azureProperties);
        }
    }
}