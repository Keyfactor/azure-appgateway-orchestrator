using System;

namespace Keyfactor.Extensions.Orchestrator.AzureAppGateway.Jobs
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class JobAttribute : Attribute
    {
        private string JobClassName { get; }

        public JobAttribute(string jobClass)
        {
            JobClassName = jobClass;
        }
        
        public virtual string JobClass => JobClassName;
    }
}