using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ranger.Services.Projects.Data
{
    public interface IProjectsRepository : IRepository
    {
        Task AddProjectAsync(string domain, string userEmail, string eventName, Project project);
        Task<IEnumerable<(Project project, int version)>> GetAllProjects();
        Task<Project> GetProjectByProjectIdAsync(string projectId);
        Task<Project> GetProjectByApiKeyAsync(string apiKey);
        Task RemoveProjectAsync(string name);
        Task UpdateProjectAsync(string domain, string userEmail, string eventName, int version, Project project);
    }
}