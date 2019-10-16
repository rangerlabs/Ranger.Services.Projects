using System;

namespace Ranger.Services.Projects.Data
{
    public interface IProject
    {
        Guid ProjectId { get; set; }
        string Name { get; set; }
        Guid ApiKey { get; set; }
        bool Enabled { get; set; }
        string Description { get; set; }
    }
}