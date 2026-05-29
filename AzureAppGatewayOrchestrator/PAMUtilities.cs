using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

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
