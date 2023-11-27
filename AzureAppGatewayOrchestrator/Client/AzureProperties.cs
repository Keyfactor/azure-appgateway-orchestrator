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

using Azure.Core;
using Azure.Identity;
using System;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrator.AzureAppGateway.Client
{
    public class AzureProperties
    {
        public string TenantId { get; set; }
        public string ApplicationId { get; set; }
        public string ClientSecret { get; set; }
        public string AzureCloud { get; set; }
        public string StorePath { get; set; }
        public List<string> TenantIdsForDiscovery { get; set; }
        public Uri AzureCloudEndpoint
        {
            get
            {
                switch (AzureCloud)
                {

                    case "china":
                        return AzureAuthorityHosts.AzureChina;
                    case "germany":
                        return AzureAuthorityHosts.AzureGermany;
                    case "government":
                        return AzureAuthorityHosts.AzureGovernment;
                    default:
                        return AzureAuthorityHosts.AzurePublicCloud;
                }
            }
        }
    }
}