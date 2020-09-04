using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{
    public interface IProjectsRepository : IRepository
    {
        Task AddProjectAsync(string userEmail, string eventName, Project project);
        Task<Project> SoftDeleteAsync(string userEmail, Guid projectId);
        Task<(Project project, int version)> GetProjectByName(string projectName, CancellationToken cancellationToken = default(CancellationToken));
        Task<IEnumerable<(Project project, int version)>> GetProjectsForUser(string email, CancellationToken cancellationToken = default(CancellationToken));
        Task<IEnumerable<(Project project, int version)>> GetAllNotDeletedProjects(CancellationToken cancellationToken = default(CancellationToken));
        Task<Project> GetProjectByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default(CancellationToken));
        Task<Project> GetProjectByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default(CancellationToken));
        Task<Project> UpdateProjectAsync(string userEmail, string eventName, int version, Project project);
        Task<(Project, string, string)> UpdateApiKeyAsync(string userEmail, ApiKeyPurposeEnum environment, int version, Guid projectId);
    }
}