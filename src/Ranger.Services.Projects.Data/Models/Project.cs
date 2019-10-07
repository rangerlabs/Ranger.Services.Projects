using System;

namespace Ranger.Services.Projects.Data
{

    public class Project : IProject
    {
        public int Version { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; }
        public Guid ApiKey { get; set; }
        public bool Enabled { get; set; }
        public string Description { get; set; }
    }
}