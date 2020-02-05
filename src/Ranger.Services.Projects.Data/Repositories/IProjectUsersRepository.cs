using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ranger.Services.Projects.Data
{
    public interface IProjectUsersRepository : IRepository
    {
        Task<IEnumerable<Guid>> AddUserToProjects(string userId, string email, IEnumerable<Guid> projectIds, string commandingUserEmail);
        Task<IEnumerable<Guid>> RemoveUserFromProjects(string userId, IEnumerable<Guid> projectIds, string commandingUserEmail);
        Task<IEnumerable<Guid>> GetAuthorizedProjectIdsForUserEmail(string email);
        Task<IEnumerable<Guid>> GetAuthorizedProjectIdsForUserId(string userId);
        Task<bool> IsUserEmailAuthorizedForProject(string email, Guid projectId);
        Task<bool> IsUserIdAuthorizedForProject(string userId, Guid projectId);
    }
}