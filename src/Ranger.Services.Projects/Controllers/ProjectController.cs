using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AutoWrapper.Wrappers;
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
        private readonly IBusPublisher busPublisher;
        private readonly TenantsHttpClient tenantsClient;
        private readonly Func<string, ProjectsRepository> projectsRepositoryFactory;
        private readonly ILogger<ProjectController> logger;
        private readonly Func<string, ProjectUsersRepository> projectUsersRepositoryFactory;
        private readonly IdentityHttpClient identityClient;
        private readonly SubscriptionsHttpClient subscriptionsClient;
        private readonly IProjectUniqueContraintRepository projectUniqueContraintRepository;

        public ProjectController(IBusPublisher busPublisher, TenantsHttpClient tenantsClient, IdentityHttpClient identityClient, SubscriptionsHttpClient subscriptionsClient, Func<string, ProjectsRepository> projectsRepositoryFactory, Func<string, ProjectUsersRepository> projectUsersRepositoryFactory, IProjectUniqueContraintRepository projectUniqueContraintRepository, ILogger<ProjectController> logger)
        {
            this.busPublisher = busPublisher;
            this.tenantsClient = tenantsClient;
            this.identityClient = identityClient;
            this.subscriptionsClient = subscriptionsClient;
            this.projectsRepositoryFactory = projectsRepositoryFactory;
            this.projectUsersRepositoryFactory = projectUsersRepositoryFactory;
            this.projectUniqueContraintRepository = projectUniqueContraintRepository;
            this.logger = logger;
        }

        ///<summary>
        /// Gets the tenant id for the provided API Key
        ///</summary>
        ///<param name="apiKey">The API dey to request the tenant's unique identifier for</param>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("projects/{apikey}/tenant-id")]
        public async Task<ApiResponse> GetTenantIdByApiKey(string apiKey)
        {
            if (apiKey.StartsWith("live.") || apiKey.StartsWith("test."))
            {
                var tenantId = await projectUniqueContraintRepository.GetTenantIdByApiKeyAsync(apiKey);
                if (String.IsNullOrWhiteSpace(tenantId))
                {
                    return new ApiResponse("No tenant was found for the provided API key", statusCode: StatusCodes.Status404NotFound);
                }
                return new ApiResponse(tenantId);
            }

            return new ApiResponse("The API key does not have a valid prefix", statusCode: StatusCodes.Status400BadRequest);
        }

        ///<summary>
        /// Gets the project unique identifiers a user is permitted to access
        ///</summary>
        ///<param name="tenantId">The tenant id the user is associated with<param>
        ///<param name="email">The user's email address<param>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpGet("/projects/{tenantId}/authorized/{email}")]
        public async Task<ApiResponse> GetProjectIdsForUser(string tenantId, [FromRoute] string email)
        {
            var repo = projectUsersRepositoryFactory(tenantId);

            IEnumerable<Guid> projectIds = new List<Guid>();
            try
            {
                projectIds = await repo.GetAuthorizedProjectIdsForUserEmail(email);
            }
            catch (Exception ex)
            {
                var message = "Failed to get Project Ids for user";
                logger.LogError(ex, message);
                throw new ApiException(message, StatusCodes.Status500InternalServerError);
            }
            return new ApiResponse("Success", projectIds);
        }

        ///<summary>
        /// Gets a project by the API key
        ///</summary>
        ///<param name="tenantId">The tenant id the user is associated with<param>
        ///<param name="email">The user's email address<param>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpGet("/projects/{tenantId}/{apiKey")]
        public async Task<ApiResponse> GetProjectByApiKey(string tenantId, string apiKey)
        {
            var repo = projectsRepositoryFactory(tenantId);

            var project = await repo.GetProjectByApiKeyAsync(apiKey);
            if (project is null)
            {
                var message = $"No project was found for the provided API key.";
                logger.LogWarning(message);
                throw new ApiException(message, StatusCodes.Status500InternalServerError);
            }
            var result = new { ProjectId = project.ProjectId, Enabled = project.Enabled, Name = project.Name };
            return new ApiResponse("Success", result);
        }

        ///<summary>
        /// Gets the projects a user is permitted to access
        ///</summary>
        ///<param name="tenantId">The tenant id the user is associated with<param>
        ///<param name="email">The user's email address<param>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpGet("/projects/{tenantId}/{email}")]
        public async Task<ApiResponse> GetAllProjectsForUser(string tenantId, string email)
        {
            var repo = projectsRepositoryFactory(tenantId);

            ApiResponse<string> apiResponse = await this.identityClient.GetUserRoleAsync(tenantId, email);
            if (!apiResponse.IsError)
            {
                var role = Enum.Parse<RolesEnum>(apiResponse.Result);

                IEnumerable<(Project project, int version)> projects;
                try
                {
                    if (role == RolesEnum.User)
                    {
                        projects = await repo.GetProjectsForUser(email);
                    }
                    else
                    {
                        projects = await repo.GetAllProjects();
                    }
                }
                catch (Exception ex)
                {
                    var message = "Failed to retrieve projects for user";
                    logger.LogError(message, ex);
                    throw new ApiException(message, StatusCodes.Status500InternalServerError);
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
                return new ApiResponse("Success", result);
            }
            throw new ApiException(apiResponse.ResponseException.ExceptionMessage.Error, statusCode: apiResponse.StatusCode);
        }

        ///<summary>
        /// Resets the API key for a given environment
        ///</summary>
        ///<param name="tenantId">The tenant id the project API key is associated with<param>
        ///<param name="projectId">The project id for the API key to reset<param>
        ///<param name="environment">The environment for the API key to reset - "live" or "test"<param>
        ///<param name="apiKeyResetModel">The model necessary to verify the key reset<param>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [HttpPut("/projects/{tenantId}/{projectId}/{environment}/reset")]
        public async Task<ApiResponse> ApiKeyReset(string tenantId, Guid projectId, string environment, ApiKeyResetModel apiKeyResetModel)
        {

            if (environment == "live" || environment == "test")
            {
                var repo = projectsRepositoryFactory(tenantId);

                try
                {
                    var (project, newApiKey) = await repo.UpdateApiKeyAsync(apiKeyResetModel.UserEmail, environment, apiKeyResetModel.Version, projectId);
                    return new ApiResponse("Success", new ProjectResponseModel
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
                    throw new ApiException(ex.Message, StatusCodes.Status409Conflict);
                }
                catch (RangerException ex)
                {
                    logger.LogError(ex.Message);
                    throw new ApiException(ex.Message, StatusCodes.Status400BadRequest);
                }
                catch (Exception ex)
                {
                    var _ = "Failed to save project stream";
                    logger.LogError(ex, _);
                    throw new ApiException(_, statusCode: StatusCodes.Status500InternalServerError);
                }
            }

            var message = "Invalid environment name. Expected either 'live' or 'test'";
            return new ApiResponse(message, statusCode: StatusCodes.Status400BadRequest);
        }

        ///<summary>
        /// Updates and existing project
        ///</summary>
        ///<param name="tenantId">The tenant id the project API key is associated with<param>
        ///<param name="projectId">The project id for the API key to reset<param>
        ///<param name="projectModel">The model necessary to update a project<param>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [HttpPut("/projects/{tenantId}/{projectId}")]
        public async Task<ApiResponse> PutProject(string tenantId, Guid projectId, PutProjectModel projectModel)
        {
            var repo = projectsRepositoryFactory(tenantId);

            var project = await repo.GetProjectByProjectIdAsync(projectId);
            if (project is null)
            {
                var message = "The project was not found. PUT can only be used to update existing projects";
                logger.LogDebug(message);
                throw new ApiException(message, StatusCodes.Status400BadRequest);
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
                throw new ApiException(ex.Message, StatusCodes.Status409Conflict);
            }
            catch (EventStreamDataConstraintException ex)
            {
                logger.LogError(ex, "Failed to save project stream because a constraint was violated");
                throw new ApiException(String.IsNullOrWhiteSpace(ex.Message) ? "Failed to save the updated project" : ex.Message, StatusCodes.Status409Conflict);
            }
            catch (NoOpException ex)
            {
                logger.LogInformation(ex.Message);
                return new ApiResponse(ex.Message, statusCode: StatusCodes.Status304NotModified);
            }
            catch (RangerException ex)
            {
                logger.LogInformation(ex.Message);
                return new ApiResponse(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (Exception ex)
            {
                var message = "Failed to save project stream";
                logger.LogError(ex, message);
                throw new ApiException(message, StatusCodes.Status500InternalServerError);
            }
            return new ApiResponse("Success", new ProjectResponseModel
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

        ///<summary>
        /// Updates and existing project
        ///</summary>
        ///<param name="tenantId">The tenant id the project API key is associated with<param>
        ///<param name="projectId">The project id for the API key to reset<param>
        ///<param name="softDeleteModel">The model necessary to delete a project<param>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [HttpDelete("/projects/{tenantId}/{projectId}")]
        public async Task<ApiResponse> SoftDeleteProject(string tenantId, Guid projectId, SoftDeleteModel softDeleteModel)
        {
            var apiResponse = await subscriptionsClient.DecrementResource(tenantId, ResourceEnum.Project);
            if (!apiResponse.IsError)
            {
                var repo = projectsRepositoryFactory(tenantId);
                try
                {
                    await repo.SoftDeleteAsync(softDeleteModel.UserEmail, projectId);
                }
                catch (ConcurrencyException ex)
                {
                    logger.LogError(ex.Message);
                    throw new ApiException(ex.Message, StatusCodes.Status409Conflict);
                }
                catch (RangerException ex)
                {
                    logger.LogInformation(ex.Message);
                    return new ApiResponse(ex.Message, statusCode: StatusCodes.Status400BadRequest);
                }
                catch (Exception ex)
                {
                    var message = "Failed to delete project stream";
                    logger.LogError(ex, message);
                    throw new ApiException(message, StatusCodes.Status500InternalServerError);
                }
                return new ApiResponse("Success");
            }
            throw new ApiException(apiResponse.ResponseException.ExceptionMessage.Error, statusCode: apiResponse.StatusCode);
        }

        ///<summary>
        /// Creates a new project
        ///</summary>
        ///<param name="tenantId">The tenant id the project API key is associated with<param>
        ///<param name="projectId">The project id for the API key to reset<param>
        ///<param name="projectModel">The model necessary to create a project<param>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [HttpPost("/projects/{tenantId}")]
        public async Task<ApiResponse> PostProject(string tenantId, PostProjectModel projectModel)
        {
            var apiResponse = await subscriptionsClient.IncrementResource(tenantId, ResourceEnum.Project);
            if (!apiResponse.IsError)
            {
                var repo = projectsRepositoryFactory(tenantId);

                return await AddNewProject(tenantId, projectModel, repo);
            }
            throw new ApiException(apiResponse.ResponseException.ExceptionMessage.Error, statusCode: apiResponse.StatusCode);
        }

        private async Task<ApiResponse> AddNewProject(string domain, PostProjectModel projectModel, IProjectsRepository repo)
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
                logger.LogError(ex, "Failed to save project stream because a constraint was violated");
                throw new ApiException(String.IsNullOrWhiteSpace(ex.Message) ? "Failed to save the updated project" : ex.Message, StatusCodes.Status409Conflict);
            }
            catch (Exception ex)
            {
                var message = "Failed to create project stream";
                logger.LogError(ex, message);
                throw new ApiException(message, StatusCodes.Status500InternalServerError);
            }

            return new ApiResponse("Success", new ProjectResponseModel
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
            }, StatusCodes.Status201Created);
        }
    }
}