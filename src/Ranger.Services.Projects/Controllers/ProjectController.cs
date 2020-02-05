using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Ranger.Common;
using Ranger.Common.Data.Exceptions;
using Ranger.InternalHttpClient;
using Ranger.RabbitMQ;
using Ranger.Services.Projects.Data;

namespace Ranger.Services.Projects
{
    [ApiController]
    public class ProjectController : ControllerBase
    {
        private readonly ITenantsClient tenantsClient;
        private readonly Func<string, ProjectsRepository> projectsRepositoryFactory;
        private readonly ILogger<ProjectController> logger;
        private readonly Func<string, ProjectUsersRepository> projectUsersRepositoryFactory;
        private readonly IIdentityClient identityClient;

        public ProjectController(ITenantsClient tenantsClient, IIdentityClient identityClient, Func<string, ProjectsRepository> projectsRepositoryFactory, Func<string, ProjectUsersRepository> projectUsersRepositoryFactory, ILogger<ProjectController> logger)
        {
            this.tenantsClient = tenantsClient;
            this.identityClient = identityClient;
            this.projectsRepositoryFactory = projectsRepositoryFactory;
            this.projectUsersRepositoryFactory = projectUsersRepositoryFactory;
            this.logger = logger;
        }

        [HttpGet("{domain}/project/authorized/{email}")]
        public async Task<IActionResult> GetProjectIdsForUser([FromRoute] string domain, [FromRoute] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                var apiErrorContent = new ApiErrorContent();
                apiErrorContent.Errors.Add($"{nameof(email)} was null or whitespace.");
                return BadRequest(apiErrorContent);
            }

            IProjectUsersRepository repo;
            try
            {
                repo = projectUsersRepositoryFactory.Invoke(domain);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }


            IEnumerable<Guid> projectIds = new List<Guid>();
            try
            {
                projectIds = await repo.GetAuthorizedProjectIdsForUserEmail(email);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to get Project Ids for user {email}.");
            }
            return Ok(projectIds);
        }

        [HttpGet("{domain}/project")]
        public async Task<IActionResult> GetProjectByApiKey([FromRoute] string domain, [FromQuery] string apiKey)
        {
            IProjectsRepository repo;
            try
            {
                repo = projectsRepositoryFactory.Invoke(domain);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var project = await repo.GetProjectByApiKeyAsync(apiKey);
            if (project is null)
            {
                var apiErrorContent = new ApiErrorContent();
                apiErrorContent.Errors.Add($"No project was found for the provided API Key.");
                return NotFound(apiErrorContent);
            }
            var result = new { ProjectId = project.ProjectId, Enabled = project.Enabled, Name = project.Name };
            return Ok(result);
        }

        [HttpGet("{domain}/project/{email}")]
        public async Task<IActionResult> GetProjects([FromRoute] string domain, [FromRoute] string email)
        {
            IProjectsRepository repo;
            try
            {
                repo = projectsRepositoryFactory.Invoke(domain);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            RolesEnum role;
            try
            {
                var roleResult = await this.identityClient.GetUserRoleAsync<RoleResponseModel>(domain, email);
                role = Enum.Parse<RolesEnum>(roleResult.Role);
            }
            catch (HttpClientException<RoleResponseModel> ex)
            {
                if ((int)ex.ApiResponse.StatusCode == StatusCodes.Status404NotFound)
                {
                    return NotFound();
                }
                else
                {
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }
            }

            IEnumerable<(Project project, int version)> projects;
            if (role == RolesEnum.User)
            {
                projects = await repo.GetProjectsForUser(email);
            }
            else
            {
                projects = await repo.GetAllProjects();
            }
            var result = projects.Select((_) =>
                               new
                               {
                                   Description = _.project.Description,
                                   Enabled = _.project.Enabled,
                                   Name = _.project.Name,
                                   ProjectId = _.project.ProjectId,
                                   LiveApiKeyPrefix = _.project.LiveApiKeyPrefix,
                                   TestApiKeyPrefix = _.project.TestApiKeyPrefix,
                                   Version = _.version
                               });
            return Ok(result);
        }

        [HttpPut("{domain}/project/{projectId}/{environment}/reset")]
        public async Task<IActionResult> ApiKeyReset([FromRoute] string domain, [FromRoute] Guid projectId, [FromRoute] string environment, ApiKeyResetModel apiKeyResetModel)
        {

            if (environment == "live" || environment == "test")
            {
                IProjectsRepository repo;
                try
                {
                    repo = projectsRepositoryFactory.Invoke(domain);
                }
                catch (Exception)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }

                try
                {
                    var (project, newApiKey) = await repo.UpdateApiKeyAsync(apiKeyResetModel.UserEmail, environment, apiKeyResetModel.Version, projectId);
                    return Ok(new ProjectResponseModel
                    {
                        ProjectId = project.ProjectId,
                        Name = project.Name,
                        Description = project.Description,
                        LiveApiKey = environment == "live" ? newApiKey : "",
                        TestApiKey = environment == "test" ? newApiKey : "",
                        LiveApiKeyPrefix = project.LiveApiKeyPrefix,
                        TestApiKeyPrefix = project.TestApiKeyPrefix,
                        Enabled = project.Enabled,
                        Version = apiKeyResetModel.Version
                    });
                }
                catch (ConcurrencyException ex)
                {
                    logger.LogError(ex.Message);
                    var errors = new ApiErrorContent();
                    errors.Errors.Add(ex.Message);
                    return Conflict(errors);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to save project stream.");
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }
            }

            var invalidEnvironmentErrors = new ApiErrorContent();
            invalidEnvironmentErrors.Errors.Add("Invalid environment name. Expected either 'live' or 'test'.");
            return BadRequest(invalidEnvironmentErrors);
        }

        [HttpPut("{domain}/project/{projectId}")]
        public async Task<IActionResult> PutProject([FromRoute] string domain, [FromRoute] Guid projectId, PutProjectModel projectModel)
        {
            IProjectsRepository repo;
            try
            {
                repo = projectsRepositoryFactory.Invoke(domain);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var project = await repo.GetProjectByProjectIdAsync(projectId);
            if (project is null)
            {
                var errors = new ApiErrorContent();
                errors.Errors.Add("The PUT method can only be used to update projects at this time.");
                return NotFound(errors);
            }
            Project updatedProject = null;

            try
            {
                updatedProject = await repo.UpdateProjectAsync(
                    projectModel.UserEmail,
                    "ProjectUpdated",
                    projectModel.Version,
                    new Project
                    {
                        ProjectId = projectId,
                        Name = projectModel.Name,
                        Description = projectModel.Description,
                        Enabled = projectModel.Enabled,
                    }
                );
            }
            catch (ConcurrencyException ex)
            {
                logger.LogError(ex.Message);
                var errors = new ApiErrorContent();
                errors.Errors.Add(ex.Message);
                return Conflict(errors);
            }
            catch (EventStreamDataConstraintException ex)
            {
                logger.LogError(ex, "Failed to save project stream because a constraint was violated.");
                var errors = new ApiErrorContent();
                errors.Errors.Add(String.IsNullOrWhiteSpace(ex.Message) ? "Failed to save the updated project." : ex.Message);
                return Conflict(errors);
            }
            catch (NoOpException ex)
            {
                logger.LogInformation(ex.Message);
                return StatusCode(StatusCodes.Status304NotModified);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save project stream.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
            return Ok(new ProjectResponseModel
            {
                ProjectId = updatedProject.ProjectId,
                Name = updatedProject.Name,
                Description = updatedProject.Description,
                Enabled = updatedProject.Enabled,
                Version = projectModel.Version,
                LiveApiKeyPrefix = updatedProject.LiveApiKeyPrefix,
                TestApiKeyPrefix = updatedProject.TestApiKeyPrefix
            });
        }

        [HttpDelete("{domain}/project/{projectId}")]
        public async Task<IActionResult> SoftDeleteProject([FromRoute] string domain, [FromRoute] Guid projectId, [FromBody] SoftDeleteModel softDeleteModel)
        {
            IProjectsRepository repo;
            try
            {
                repo = projectsRepositoryFactory.Invoke(domain);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            try
            {
                await repo.SoftDeleteAsync(softDeleteModel.UserEmail, projectId);
                return NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete project stream.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("{domain}/project")]
        public async Task<IActionResult> PostProject([FromRoute] string domain, PostProjectModel projectModel)
        {
            IProjectsRepository repo;
            try
            {
                repo = projectsRepositoryFactory.Invoke(domain);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return await AddNewProject(domain, projectModel, repo);
        }

        private async Task<IActionResult> AddNewProject(string domain, PostProjectModel projectModel, IProjectsRepository repo)
        {
            string liveApiKeyGuid;
            string testApiKeyGuid;
            string liveApiKeyPrefix;
            string testApiKeyPrefix;

            var adequatelyDifferentPrefixes = false;
            do
            {
                liveApiKeyGuid = Guid.NewGuid().ToString();
                testApiKeyGuid = Guid.NewGuid().ToString();
                liveApiKeyPrefix = liveApiKeyGuid.Substring(0, 6);
                testApiKeyPrefix = testApiKeyGuid.Substring(0, 6);
                liveApiKeyGuid = "live." + liveApiKeyGuid;
                testApiKeyGuid = "test." + testApiKeyGuid;
                var liveApiKeyPrefixDistinct = liveApiKeyPrefix.Distinct();
                var testApiKeyPrefixDistinct = testApiKeyPrefix.Distinct();

                IEnumerable<char> shorterDistinctPrefix;
                string longerPrefix;
                if (liveApiKeyPrefixDistinct.Count() <= testApiKeyPrefixDistinct.Count())
                {
                    shorterDistinctPrefix = liveApiKeyPrefixDistinct;
                    longerPrefix = testApiKeyPrefix;
                }
                else
                {
                    shorterDistinctPrefix = testApiKeyPrefixDistinct;
                    longerPrefix = liveApiKeyPrefix;
                }

                int charDiffCount = 0;
                foreach (var character in shorterDistinctPrefix)
                {
                    if (longerPrefix.Count(testChar => testChar != character) == longerPrefix.Count())
                    {
                        charDiffCount++;
                        if (charDiffCount >= 2)
                        {
                            break;
                        }
                    }

                }
                adequatelyDifferentPrefixes = charDiffCount == 2;
            }
            while (!adequatelyDifferentPrefixes);

            var hashedLiveApiKey = Crypto.GenerateSHA512Hash(liveApiKeyGuid);
            var hashedTestApiKey = Crypto.GenerateSHA512Hash(testApiKeyGuid);

            var project = new Project
            {
                ProjectId = Guid.NewGuid(),
                Name = projectModel.Name,
                HashedLiveApiKey = hashedLiveApiKey,
                HashedTestApiKey = hashedTestApiKey,
                LiveApiKeyPrefix = "live." + liveApiKeyPrefix,
                TestApiKeyPrefix = "test." + testApiKeyPrefix,
                Enabled = projectModel.Enabled,
                Description = projectModel.Description,
            };
            try
            {
                await repo.AddProjectAsync(projectModel.UserEmail, "ProjectCreated", project);
            }
            catch (EventStreamDataConstraintException ex)
            {
                logger.LogError(ex, "Failed to save project stream because a constraint was violated.");
                var errors = new ApiErrorContent();
                errors.Errors.Add(ex.Message);
                return Conflict(errors);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save project stream.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Ok(new ProjectResponseModel
            {
                ProjectId = project.ProjectId,
                Name = project.Name,
                Description = project.Description,
                LiveApiKey = liveApiKeyGuid,
                TestApiKey = testApiKeyGuid,
                LiveApiKeyPrefix = project.LiveApiKeyPrefix,
                TestApiKeyPrefix = project.TestApiKeyPrefix,
                Enabled = project.Enabled,
                Version = 0
            });
        }
    }
}