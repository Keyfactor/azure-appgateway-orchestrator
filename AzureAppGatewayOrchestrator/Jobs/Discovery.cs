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

        public JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate sdu)
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
                sdu(GatewayClient.DiscoverAppGateways());
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job:\n {0}", ex.Message);
                result.FailureMessage = ex.Message;
            }

            return result;
        }
    }
}