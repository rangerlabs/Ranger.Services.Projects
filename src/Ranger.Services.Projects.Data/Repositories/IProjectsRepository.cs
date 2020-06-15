using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{
    public interface IProjectsRepository : IRepository
    {
        Task AddProjectAsync(string userEmail, string eventName, Project project);
        Task SoftDeleteAsync(string userEmail, Guid projectId);
        Task<(Project project, int version)> GetProjectByName(string projectName);
        Task<IEnumerable<(Project project, int version)>> GetProjectsForUser(string email);
        Task<IEnumerable<(Project project, int version)>> GetAllProjects();
        Task<Project> GetProjectByProjectIdAsync(Guid projectId);
        Task<Project> GetProjectByApiKeyAsync(string apiKey);
        Task<Project> UpdateProjectAsync(string userEmail, string eventName, int version, Project project);
        Task<(Project, string)> UpdateApiKeyAsync(string userEmail, ApiKeyPurposeEnum environment, int version, Guid projectId);
    }
}