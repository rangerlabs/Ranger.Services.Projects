using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    [MessageNamespace("projects")]
    public class CreateProjectRejected : IRejectedEvent
    {
        public CreateProjectRejected(string reason, string code)
        {
            this.Reason = reason;
            this.Code = code;

        }
        public string Reason { get; }

        public string Code { get; }
    }
}