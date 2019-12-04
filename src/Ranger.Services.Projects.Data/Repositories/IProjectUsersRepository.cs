using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ranger.Services.Projects.Data
{
    public interface IProjectUsersRepository : IRepository
    {
        Task<IEnumerable<string>> AddUserToProjects(string userId, string email, IEnumerable<string> projectIds, string commandingUserEmail);
        Task<IEnumerable<string>> RemoveUserFromProjects(string userId, IEnumerable<string> projectIds, string commandingUserEmail);
        Task<IEnumerable<string>> GetAuthorizedProjectIdsForUserEmail(string email);
        Task<IEnumerable<string>> GetAuthorizedProjectIdsForUserId(string userId);
        Task<bool> IsUserEmailAuthorizedForProject(string email, string projectId);
        Task<bool> IsUserIdAuthorizedForProject(string userId, string projectId);
    }
}