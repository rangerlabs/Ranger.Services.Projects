using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{

    public class ProjectUsersRepository : BaseRepository<ProjectUsersRepository>, IProjectUsersRepository
    {
        private readonly ContextTenant contextTenant;
        private readonly ILogger<BaseRepository<ProjectUsersRepository>> logger;

        public ProjectUsersRepository(ContextTenant contextTenant, ProjectsDbContext.Factory context, CloudSqlOptions cloudSqlOptions, ILogger<BaseRepository<ProjectUsersRepository>> logger) : base(contextTenant, context, cloudSqlOptions, logger)
        {
            this.logger = logger;
            this.contextTenant = contextTenant;
        }

        public async Task<IEnumerable<string>> GetAuthorizedProjectIdsForUserEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new System.ArgumentException($"{nameof(email)} was null or whitespace.");
            }
            return await Context.ProjectUsers.Where(_ => _.Email == email).Select(_ => _.ProjectId.ToString()).ToListAsync();
        }

        public async Task<IEnumerable<string>> GetAuthorizedProjectIdsForUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new System.ArgumentException($"{nameof(userId)} was null or whitespace.");
            }
            return await Context.ProjectUsers.Where(_ => _.UserId == userId).Select(_ => _.ProjectId.ToString()).ToListAsync();
        }

        public async Task<bool> IsUserEmailAuthorizedForProject(string email, string projectId)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new System.ArgumentException($"{nameof(email)} was null or whitespace.");
            }
            if (string.IsNullOrWhiteSpace(projectId))
            {
                throw new System.ArgumentException($"{nameof(projectId)} was null or whitespace.");
            }
            return await Context.ProjectUsers.AnyAsync(_ => _.ProjectId == Guid.Parse(projectId) && _.Email == email);
        }

        public async Task<bool> IsUserIdAuthorizedForProject(string userId, string projectId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new System.ArgumentException($"{nameof(userId)} was null or whitespace.");
            }
            if (string.IsNullOrWhiteSpace(projectId))
            {
                throw new System.ArgumentException($"{nameof(projectId)} was null or whitespace.");
            }
            return await Context.ProjectUsers.AnyAsync(_ => _.ProjectId == Guid.Parse(projectId) && _.UserId == userId);
        }

        public async Task<IEnumerable<string>> AddUserToProjects(string userId, string email, IEnumerable<string> projectIds, string commandingUserEmail)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new System.ArgumentException($"{nameof(userId)} was null or whitespace.");
            }
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new System.ArgumentException($"{nameof(email)} was null or whitespace.");
            }
            if (projectIds is null)
            {
                throw new System.ArgumentException($"{nameof(projectIds)} was null or whitespace.");
            }
            if (projectIds.Count() == 0)
            {
                throw new ArgumentOutOfRangeException($"{nameof(projectIds)} contained no projectIds.");
            }
            if (string.IsNullOrWhiteSpace(commandingUserEmail))
            {
                throw new System.ArgumentException($"{nameof(commandingUserEmail)} was null or whitespace.");
            }

            var projectUsersToAdd = new List<ProjectUser>();
            var invalidProjects = new List<string>();
            foreach (var projectId in projectIds)
            {
                try
                {
                    var projectUser = new ProjectUser
                    {
                        DatabaseUsername = this.contextTenant.DatabaseUsername,
                        ProjectId = Guid.Parse(projectId),
                        UserId = userId,
                        Email = email,
                        InsertedAt = DateTime.UtcNow,
                        InsertedBy = commandingUserEmail
                    };
                    projectUsersToAdd.Add(projectUser);
                }
                catch (FormatException ex)
                {
                    logger.LogWarning(ex, $"The Project Id {projectId} is not a valid format.");
                    invalidProjects.Add(projectId);
                }

            }

            Context.ProjectUsers.AddRange(projectUsersToAdd);
            await Context.SaveChangesAsync();
            return invalidProjects;
        }

        public async Task<IEnumerable<string>> RemoveUserFromProjects(string userId, IEnumerable<string> projectIds, string commandingUserEmail)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new System.ArgumentException($"{nameof(userId)} was null or whitespace.");
            }
            if (projectIds is null)
            {
                throw new System.ArgumentException($"{nameof(projectIds)} was null or whitespace.");
            }
            if (projectIds.Count() == 0)
            {
                throw new ArgumentOutOfRangeException($"{nameof(projectIds)} contained no projectIds.");
            }
            if (string.IsNullOrWhiteSpace(commandingUserEmail))
            {
                throw new System.ArgumentException($"{nameof(commandingUserEmail)} was null or whitespace.");
            }

            var guidProjectIds = new List<Guid>();
            var invalidProjects = new List<string>();
            foreach (var projectId in projectIds)
            {
                try
                {
                    guidProjectIds.Add(Guid.Parse(projectId));
                }
                catch (FormatException ex)
                {
                    logger.LogWarning(ex, $"The Project Id {projectId} is not a valid format.");
                    invalidProjects.Add(projectId);
                }
            }
            Context.ProjectUsers.RemoveRange(await Context.ProjectUsers.Where(_ => _.UserId == userId && guidProjectIds.Contains(_.ProjectId)).ToListAsync());
            await Context.SaveChangesAsync();
            return invalidProjects;
        }
    }
}