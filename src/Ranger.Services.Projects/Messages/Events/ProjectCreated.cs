using System;
using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    [MessageNamespace("projects")]
    public class ProjectCreated : IEvent
    {

        public ProjectCreated(string tenantId, Guid projectId, string name, string description, string liveApiKeyPrefix, string testApiKeyPrefix, string projectApiKeyPrefix, bool enabled)
        {
            this.TenantId = tenantId;
            this.ProjectId = projectId;
            this.Name = name;
            this.Description = description;
            this.LiveApiKeyPrefix = liveApiKeyPrefix;
            this.TestApiKeyPrefix = testApiKeyPrefix;
            this.ProjectApiKeyPrefix = projectApiKeyPrefix;
            this.Enabled = enabled;

        }
        public string TenantId { get; }
        public Guid ProjectId { get; }
        public string Name { get; }
        public string Description { get; }
        public string LiveApiKeyPrefix { get; }
        public string TestApiKeyPrefix { get; }
        public string ProjectApiKeyPrefix { get; }
        public bool Enabled { get; }
    }
}