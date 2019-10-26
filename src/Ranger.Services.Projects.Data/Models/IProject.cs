using System;

namespace Ranger.Services.Projects.Data
{
    public interface IProject
    {
        Guid ProjectId { get; set; }
        string Name { get; set; }
        string HashedLiveApiKey { get; set; }
        string HashedTestApiKey { get; set; }
        string LiveApiKeyPrefix { get; set; }
        string TestApiKeyPrefix { get; set; }
        bool Enabled { get; set; }
        string Description { get; set; }
    }
}