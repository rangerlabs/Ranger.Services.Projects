using System;

namespace Ranger.Services.Projects.Data
{
    public interface IProject
    {
        Guid Id { get; set; }
        string Name { get; set; }
        string HashedLiveApiKey { get; set; }
        string HashedTestApiKey { get; set; }
        string HashedProjectApiKey { get; set; }
        string LiveApiKeyPrefix { get; set; }
        string TestApiKeyPrefix { get; set; }
        string ProjectApiKeyPrefix { get; set; }
        bool Enabled { get; set; }
        string Description { get; set; }
        bool Deleted { get; set; }
    }
}