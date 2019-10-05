using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    [MessageNamespace("projects")]
    public class ProjectCreated : IEvent
    {

        public ProjectCreated(string domain, string name, string description, string apiKey)
        {
            this.Domain = domain;
            this.Name = name;
            this.Description = description;
            this.ApiKey = apiKey;

        }
        public string Domain { get; }
        public string Name { get; }
        public string Description { get; }
        public string ApiKey { get; }
    }
}