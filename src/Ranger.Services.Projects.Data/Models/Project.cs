using System;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{

    public class Project : IProject
    {
        public int Version { get; set; }
        [JsonUniqueConstraintAttribute]
        public Guid ProjectId { get; set; }
        [JsonUniqueConstraintAttribute]
        public string Name { get; set; }
        [JsonUniqueConstraintAttribute]
        public Guid ApiKey { get; set; }
        public bool Enabled { get; set; }
        public string Description { get; set; }
    }
}