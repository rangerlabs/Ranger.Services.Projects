using System;
using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    [MessageNamespace("projects")]
    public class ProjectCreated : IEvent
    {

        public ProjectCreated(string domain, Guid projectId, string name, string description, string liveApiKeyPrefix, string testApiKeyPrefix, bool enabled)
        {
            this.Domain = domain;
            this.ProjectId = projectId;
            this.Name = name;
            this.Description = description;
            this.LiveApiKeyPrefix = liveApiKeyPrefix;
            this.TestApiKeyPrefix = testApiKeyPrefix;
            this.Enabled = enabled;

        }
        public string Domain { get; }
        public Guid ProjectId { get; }
        public string Name { get; }
        public string Description { get; }
        public string LiveApiKeyPrefix { get; }
        public string TestApiKeyPrefix { get; }
        public bool Enabled { get; }
    }
}