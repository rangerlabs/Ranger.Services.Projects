using System;
using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    public class ProjectResponseModel : IEvent
    {

        public ProjectResponseModel(string projectId, string name, string description, string apiKey, bool enabled, int version)
        {
            this.ProjectId = projectId;
            this.Name = name;
            this.Description = description;
            this.ApiKey = apiKey;
            this.Enabled = enabled;
            this.Version = version;

        }
        public string ProjectId { get; }
        public string Name { get; }
        public string Description { get; }
        public string ApiKey { get; }
        public bool Enabled { get; }
        public int Version { get; }
    }
}