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

    public class ProjectUsersRepository : IProjectUsersRepository
    {
        private readonly ContextTenant contextTenant;
        private readonly ProjectsDbContext context;
        private readonly ILogger<ProjectUsersRepository> logger;

        public ProjectUsersRepository(ContextTenant contextTenant, ProjectsDbContext context, ILogger<ProjectUsersRepository> logger)
        {
            this.contextTenant = contextTenant;
            this.context = context;
            this.logger = logger;
        }

        public async Task<IEnumerable<Guid>> GetAuthorizedProjectIdsForUserEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new System.ArgumentException($"{nameof(email)} was null or whitespace.");
            }
            return await this.context.ProjectUsers.Where(_ => _.Email == email).Select(_ => _.ProjectId).ToListAsync();
        }

        public async Task<IEnumerable<Guid>> GetAuthorizedProjectIdsForUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new System.ArgumentException($"{nameof(userId)} was null or whitespace.");
            }
            return await this.context.ProjectUsers.Where(_ => _.UserId == userId).Select(_ => _.ProjectId).ToListAsync();
        }

        public async Task<bool> IsUserEmailAuthorizedForProject(string email, Guid projectId)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new System.ArgumentException($"{nameof(email)} was null or whitespace.");
            }

            return await this.context.ProjectUsers.AnyAsync(_ => _.ProjectId == projectId && _.Email == email);
        }

        public async Task<bool> IsUserIdAuthorizedForProject(string userId, Guid projectId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new System.ArgumentException($"{nameof(userId)} was null or whitespace.");
            }

            return await this.context.ProjectUsers.AnyAsync(_ => _.ProjectId == projectId && _.UserId == userId);
        }

        public async Task<IEnumerable<Guid>> AddUserToProjects(string userId, string email, IEnumerable<Guid> projectIds, string commandingUserEmail)
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
            var invalidProjects = new List<Guid>();
            foreach (var projectId in projectIds)
            {
                try
                {
                    var projectUser = new ProjectUser
                    {
                        TenantId = this.contextTenant.TenantId,
                        ProjectId = projectId,
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

            this.context.ProjectUsers.AddRange(projectUsersToAdd);
            await this.context.SaveChangesAsync();
            return invalidProjects;
        }

        public async Task<IEnumerable<Guid>> RemoveUserFromProjects(string userId, IEnumerable<Guid> projectIds, string commandingUserEmail)
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
            var invalidProjects = new List<Guid>();
            foreach (var projectId in projectIds)
            {
                try
                {
                    guidProjectIds.Add(projectId);
                }
                catch (FormatException ex)
                {
                    logger.LogWarning(ex, $"The Project Id {projectId} is not a valid format.");
                    invalidProjects.Add(projectId);
                }
            }
            this.context.ProjectUsers.RemoveRange(await this.context.ProjectUsers.Where(_ => _.UserId == userId && guidProjectIds.Contains(_.ProjectId)).ToListAsync());
            await this.context.SaveChangesAsync();
            return invalidProjects;
        }
    }
}