using System;
using Ranger.Common;
using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{

    [MessageNamespace("subscriptions")]
    public class DecrementResourceCount : ICommand
    {
        public string TenantId { get; }
        public ResourceEnum Resource { get; }

        public DecrementResourceCount(string tenantId, ResourceEnum resource)
        {
            if (String.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentException($"{nameof(tenantId)} was null or whitespace.");
            }
            this.TenantId = tenantId;
            this.Resource = resource;
        }
    }
}