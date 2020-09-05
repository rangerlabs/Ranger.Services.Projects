using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ranger.Common;
using Ranger.InternalHttpClient;
using Ranger.Services.Projects.Data;
using StackExchange.Redis;

namespace Ranger.Services.Projects
{
    public class ProjectsService
    {
        private readonly Func<string, ProjectsRepository> _projectsRepositoryFactory;
        private readonly Func<string, ProjectUsersRepository> _projectUsersRepositoryFactory;
        private readonly IProjectUniqueContraintRepository _projectUniqueContraintRepository;
        private readonly IIdentityHttpClient _identityClient;
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly ILogger<ProjectsService> _logger;

        public ProjectsService(
            Func<string, ProjectsRepository> projectsRepositoryFactory,
            Func<string, ProjectUsersRepository> projectUsersRepositoryFactory,
            IProjectUniqueContraintRepository projectUniqueContraintRepository,
            IIdentityHttpClient identityClient,
            IConnectionMultiplexer connectionMultiplexer,
            ILogger<ProjectsService> logger
            )
        {
            this._projectsRepositoryFactory = projectsRepositoryFactory;
            this._projectUsersRepositoryFactory = projectUsersRepositoryFactory;
            this._projectUniqueContraintRepository = projectUniqueContraintRepository;
            this._identityClient = identityClient;
            this._connectionMultiplexer = connectionMultiplexer;
            this._logger = logger;
        }

        public async Task<IEnumerable<ProjectResponseModel>> GetAllProjects(string tenantId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var repo = _projectsRepositoryFactory(tenantId);

            var projects = await repo.GetAllNotDeletedProjects(cancellationToken);
            return projects.Select(_ => new ProjectResponseModel()
            {
                Description = _.project.Description,
                Enabled = _.project.Enabled,
                Name = _.project.Name,
                Id = _.project.Id,
                LiveApiKeyPrefix = _.project.LiveApiKeyPrefix,
                TestApiKeyPrefix = _.project.TestApiKeyPrefix,
                ProjectApiKeyPrefix = _.project.ProjectApiKeyPrefix,
                Version = _.version
            });
        }

        public async Task<Project> GetProjectByApiKey(string tenantId, string apiKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            var repo = _projectsRepositoryFactory(tenantId);
            return await repo.GetProjectByApiKeyAsync(apiKey, cancellationToken);
        }

        public async Task<ProjectResponseModel> GetProjectByName(string tenantId, string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            var projects = await GetAllProjects(tenantId, cancellationToken);
            return projects.Where(_ => _.Name == name).SingleOrDefault();
        }

        public async Task<IEnumerable<ProjectResponseModel>> GetProjectsForUser(string tenantId, string email, CancellationToken cancellationToken = default(CancellationToken))
        {
            var repo = _projectsRepositoryFactory(tenantId);

            RangerApiResponse<string> apiResponse = await this._identityClient.GetUserRoleAsync(tenantId, email, cancellationToken);
            var role = Enum.Parse<RolesEnum>(apiResponse.Result);

            IEnumerable<(Project project, int version)> projects;
            if (role == RolesEnum.User)
            {
                projects = await repo.GetProjectsForUser(email, cancellationToken);
            }
            else
            {
                projects = await repo.GetAllNotDeletedProjects(cancellationToken);
            }

            return projects.Select(_ => new ProjectResponseModel()
            {
                Description = _.project.Description,
                Enabled = _.project.Enabled,
                Name = _.project.Name,
                Id = _.project.Id,
                LiveApiKeyPrefix = _.project.LiveApiKeyPrefix,
                TestApiKeyPrefix = _.project.TestApiKeyPrefix,
                ProjectApiKeyPrefix = _.project.ProjectApiKeyPrefix,
                Version = _.version
            });
        }

        public async Task<string> GetTenantIdOrDefaultFromRedisByHashedApiKeyAsync(string hashedApiKey)
        {
            var redisDb = _connectionMultiplexer.GetDatabase();
            return await redisDb.StringGetAsync(RedisKeys.GetTenantId(hashedApiKey));
        }

        public async Task SetTenantIdInRedisByHashedApiKey(string hashedApiKey, string tenantId)
        {
            var redisDb = _connectionMultiplexer.GetDatabase();
            await redisDb.StringSetAsync(RedisKeys.GetTenantId(hashedApiKey), tenantId);
            _logger.LogDebug("TenantId added to cache");
        }

        public async Task RemoveTenantIdFromRedisByHashedApiKey(string hashedApiKey)
        {
            var redisDb = _connectionMultiplexer.GetDatabase();
            await redisDb.KeyDeleteAsync(RedisKeys.GetTenantId(hashedApiKey));
            _logger.LogDebug("TenantId removed from cache");
        }
    }
}