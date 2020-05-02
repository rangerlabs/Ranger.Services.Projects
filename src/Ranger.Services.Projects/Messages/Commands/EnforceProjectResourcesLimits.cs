using Ranger.Common;
using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    [MessageNamespace("geofences")]
    public class EnforceProjectResourceLimits : ICommand
    {
        public string TenantId;
        public int Limit;
        public EnforceProjectResourceLimits(string tenantId, int limit)
        {
            this.Limit = limit;
            this.TenantId = tenantId;
        }
    }
}