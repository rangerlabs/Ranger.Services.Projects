using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IdentityHttpClient identityClient;

        public ProjectsService(
            Func<string, ProjectsRepository> projectsRepositoryFactory,
            Func<string, ProjectUsersRepository> projectUsersRepositoryFactory,
            IProjectUniqueContraintRepository projectUniqueContraintRepository,
            IdentityHttpClient identityClient)
        {
            this.projectsRepositoryFactory = projectsRepositoryFactory;
            this.projectUsersRepositoryFactory = projectUsersRepositoryFactory;
            this.projectUniqueContraintRepository = projectUniqueContraintRepository;
            this.identityClient = identityClient;
        }

        public async Task<IEnumerable<ProjectResponseModel>> GetAllProjects(string tenantId)
        {
            var repo = projectsRepositoryFactory(tenantId);

            var projects = await repo.GetAllNotDeletedProjects();
            return projects.Select(_ => new ProjectResponseModel()
            {
                Description = _.project.Description,
                Enabled = _.project.Enabled,
                Name = _.project.Name,
                ProjectId = _.project.ProjectId,
                LiveApiKeyPrefix = _.project.LiveApiKeyPrefix,
                TestApiKeyPrefix = _.project.TestApiKeyPrefix,
                ProjectApiKeyPrefix = _.project.ProjectApiKeyPrefix,
                Version = _.version
            });
        }

        public async Task<Project> GetProjectByApiKey(string tenantId, string apiKey)
        {
            var repo = projectsRepositoryFactory(tenantId);
            return await repo.GetProjectByApiKeyAsync(apiKey);
        }

        public async Task<ProjectResponseModel> GetProjectByName(string tenantId, string name)
        {
            var projects = await GetAllProjects(tenantId);
            return projects.Where(_ => _.Name == name).SingleOrDefault();
        }

        public async Task<IEnumerable<ProjectResponseModel>> GetProjectsForUser(string tenantId, string email)
        {
            var repo = projectsRepositoryFactory(tenantId);

            RangerApiResponse<string> apiResponse = await this.identityClient.GetUserRoleAsync(tenantId, email);
            var role = Enum.Parse<RolesEnum>(apiResponse.Result);

            IEnumerable<(Project project, int version)> projects;
            if (role == RolesEnum.User)
            {
                projects = await repo.GetProjectsForUser(email);
            }
            else
            {
                projects = await repo.GetAllNotDeletedProjects();
            }

            return projects.Select(_ => new ProjectResponseModel()
            {
                Description = _.project.Description,
                Enabled = _.project.Enabled,
                Name = _.project.Name,
                ProjectId = _.project.ProjectId,
                LiveApiKeyPrefix = _.project.LiveApiKeyPrefix,
                TestApiKeyPrefix = _.project.TestApiKeyPrefix,
                ProjectApiKeyPrefix = _.project.ProjectApiKeyPrefix,
                Version = _.version
            });
        }
    }
}