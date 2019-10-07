using System;
using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    public class ProjectResponseModel : IEvent
    {

        public ProjectResponseModel(string domain, Guid projectId, string name, string description, string apiKey, bool enabled)
        {
            this.Domain = domain;
            this.ProjectId = projectId;
            this.Name = name;
            this.Description = description;
            this.ApiKey = apiKey;
            this.Enabled = enabled;

        }
        public string Domain { get; }
        public Guid ProjectId { get; }
        public string Name { get; }
        public string Description { get; }
        public string ApiKey { get; }
        public bool Enabled { get; }
    }
}