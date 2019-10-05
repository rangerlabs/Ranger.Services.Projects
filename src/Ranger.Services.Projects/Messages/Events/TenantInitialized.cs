using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    [MessageNamespaceAttribute("projects")]
    public class TenantInitialized : IEvent
    {
        public TenantInitialized() { }
    }
}