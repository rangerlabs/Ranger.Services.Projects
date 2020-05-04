using System;
using System.Collections.Generic;
using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    [MessageNamespace("projects")]
    public class ProjectResourceLimitsEnforced : IEvent
    {
        public List<(string tenantId, IEnumerable<Guid> remainingProjectIds)> TenantRemainingProjects { get; }

        public ProjectResourceLimitsEnforced(List<(string tenantId, IEnumerable<Guid> remainingProjectIds)> tenantRemainingProjects)
        {
            this.TenantRemainingProjects = tenantRemainingProjects;
        }
    }
}