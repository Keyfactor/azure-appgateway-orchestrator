
//  Copyright 2026 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System.Runtime.CompilerServices;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("AzureAppGatewayOrchestrator.Tests")]

namespace AzureAppGatewayOrchestrator
{
    internal class PAMUtilities
    {
        internal static string ResolvePAMField(ILogger logger, IPAMSecretResolver resolver, string description, string key)
        {
            logger.MethodEntry();

            if (resolver == null)
            {
                logger.LogTrace($"No PAM Resolver configured - using {description} value directly from store configuration.");
                logger.MethodExit();
                return key;
            }

            logger.LogDebug($"Fetching {description} value from PAM");
            var value = resolver.Resolve(key);
            logger.LogDebug($"Successfully fetched {description} value from PAM");
            logger.MethodExit();
            return value;
        }
    }
}
