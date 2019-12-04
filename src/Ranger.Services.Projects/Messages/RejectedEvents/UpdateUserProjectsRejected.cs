using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    [MessageNamespace("projects")]
    public class UpdateUserProjectsRejected : IRejectedEvent
    {
        public UpdateUserProjectsRejected(string reason, string code)
        {
            this.Reason = reason;
            this.Code = code;

        }
        public string Reason { get; }

        public string Code { get; }
    }
}