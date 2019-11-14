using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ranger.Services.Projects.Data
{
    public interface IProjectsRepository : IRepository
    {
        Task AddProjectAsync(string userEmail, string eventName, Project project);
        Task RemoveProjectAsync(string name);
        Task SoftDeleteAsync(string userEmail, string projectId);
        Task<IEnumerable<(Project project, int version)>> GetAllProjects();
        Task<Project> GetProjectByProjectIdAsync(string projectId);
        Task<Project> GetProjectByApiKeyAsync(string apiKey);
        Task<Project> UpdateProjectAsync(string userEmail, string eventName, int version, Project project);
        Task<(Project, string)> UpdateApiKeyAsync(string userEmail, string environment, int version, string projectName);
    }
}