using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ranger.Services.Projects.Data
{
    public interface IProjectsRepository : IRepository
    {
        Task AddProjectAsync(string domain, string userEmail, string eventName, Project project);
        Task<IEnumerable<Project>> GetAllProjects();
        Task<Project> GetProjectByApiKeyAsync(string apiKey);
        Task<Project> GetProjectByProjectIdAsync(string domain, string projectId);
        Task RemoveProjectAsync(string domain, string projectId);
        Task UpdateProjectAsync(string domain, string userEmail, string eventName, Project project);
    }
}