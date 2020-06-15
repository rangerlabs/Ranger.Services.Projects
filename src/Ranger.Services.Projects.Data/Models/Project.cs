using System;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{
    public class Project : IProject
    {
        public Guid ProjectId { get; set; }
        public string Name { get; set; }
        public string HashedLiveApiKey { get; set; }
        public string HashedTestApiKey { get; set; }
        public string HashedProjectApiKey { get; set; }
        public string LiveApiKeyPrefix { get; set; }
        public string TestApiKeyPrefix { get; set; }
        public string ProjectApiKeyPrefix { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Deleted { get; set; } = false;
        public DateTime CreatedOn { get; set; }
    }
}