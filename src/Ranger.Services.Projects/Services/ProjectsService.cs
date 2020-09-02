using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ranger.Common;
using Ranger.InternalHttpClient;
using Ranger.Services.Projects.Data;

namespace Ranger.Services.Projects
{
    public class ProjectsService
    {
        private readonly Func<string, ProjectsRepository> projectsRepositoryFactory;
        private readonly Func<string, ProjectUsersRepository> projectUsersRepositoryFactory;
        private readonly IProjectUniqueContraintRepository projectUniqueContraintRepository;
        private readonly IIdentityHttpClient identityClient;

        public ProjectsService(
            Func<string, ProjectsRepository> projectsRepositoryFactory,
            Func<string, ProjectUsersRepository> projectUsersRepositoryFactory,
            IProjectUniqueContraintRepository projectUniqueContraintRepository,
            IIdentityHttpClient identityClient)
        {
            this.projectsRepositoryFactory = projectsRepositoryFactory;
            this.projectUsersRepositoryFactory = projectUsersRepositoryFactory;
            this.projectUniqueContraintRepository = projectUniqueContraintRepository;
            this.identityClient = identityClient;
        }

        public async Task<IEnumerable<ProjectResponseModel>> GetAllProjects(string tenantId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var repo = projectsRepositoryFactory(tenantId);

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
            var repo = projectsRepositoryFactory(tenantId);
            return await repo.GetProjectByApiKeyAsync(apiKey, cancellationToken);
        }

        public async Task<ProjectResponseModel> GetProjectByName(string tenantId, string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            var projects = await GetAllProjects(tenantId, cancellationToken);
            return projects.Where(_ => _.Name == name).SingleOrDefault();
        }

        public async Task<IEnumerable<ProjectResponseModel>> GetProjectsForUser(string tenantId, string email, CancellationToken cancellationToken = default(CancellationToken))
        {
            var repo = projectsRepositoryFactory(tenantId);

            RangerApiResponse<string> apiResponse = await this.identityClient.GetUserRoleAsync(tenantId, email, cancellationToken);
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
    }
}