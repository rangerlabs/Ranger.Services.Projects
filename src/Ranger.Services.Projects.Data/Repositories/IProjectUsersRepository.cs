using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ranger.Services.Projects.Data
{
    public interface IProjectUsersRepository : IRepository
    {
        Task<IEnumerable<Guid>> AddUserToProjects(string userId, string email, IEnumerable<Guid> projectIds, string commandingUserEmail);
        Task<IEnumerable<Guid>> RemoveUserFromProjects(string userId, IEnumerable<Guid> projectIds, string commandingUserEmail);
        Task<IEnumerable<Guid>> GetAuthorizedProjectIdsForUserEmail(string email, CancellationToken cancellationToken = default(CancellationToken));
        Task<IEnumerable<Guid>> GetAuthorizedProjectIdsForUserId(string userId, CancellationToken cancellationToken = default(CancellationToken));
        Task<bool> IsUserEmailAuthorizedForProject(string email, Guid projectId, CancellationToken cancellationToken = default(CancellationToken));
        Task<bool> IsUserIdAuthorizedForProject(string userId, Guid projectId, CancellationToken cancellationToken = default(CancellationToken));
    }
}