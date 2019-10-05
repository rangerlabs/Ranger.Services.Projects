using System.Threading.Tasks;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{
    public interface IProjectsRepository : IRepository
    {
        Task AddProjectAsync(Project project);
        Task<Project> GetProjectByApiKeyAsync(string apiKey);
        Task<Project> GetProjectByNameAsync(string name);
        Task RemoveProjectAsync(string name);
        Task UpdateProjectAsync(Project project);
    }
}