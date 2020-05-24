using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWrapper.Wrappers;
using Microsoft.AspNetCore.Authorization;
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
    [ApiVersion("1.0")]
    [Authorize]
    public class ProjectController : ControllerBase
    {
        private readonly IBusPublisher busPublisher;
        private readonly Func<string, ProjectsRepository> projectsRepositoryFactory;
        private readonly ILogger<ProjectController> logger;
        private readonly Func<string, ProjectUsersRepository> projectUsersRepositoryFactory;
        private readonly IdentityHttpClient identityClient;
        private readonly SubscriptionsHttpClient subscriptionsClient;
        private readonly IProjectUniqueContraintRepository projectUniqueContraintRepository;
        private readonly ProjectsService projectsService;

        public ProjectController(
            IBusPublisher busPublisher,
            IdentityHttpClient identityClient,
            SubscriptionsHttpClient subscriptionsClient,
            Func<string, ProjectsRepository> projectsRepositoryFactory,
            Func<string, ProjectUsersRepository> projectUsersRepositoryFactory,
            IProjectUniqueContraintRepository projectUniqueContraintRepository,
            ProjectsService projectsService,
            ILogger<ProjectController> logger)
        {
            this.busPublisher = busPublisher;
            this.identityClient = identityClient;
            this.subscriptionsClient = subscriptionsClient;
            this.projectsRepositoryFactory = projectsRepositoryFactory;
            this.projectUsersRepositoryFactory = projectUsersRepositoryFactory;
            this.projectUniqueContraintRepository = projectUniqueContraintRepository;
            this.projectsService = projectsService;
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
                    throw new ApiException("No tenant was found for the specified tenant id", StatusCodes.Status404NotFound);
                }
                return new ApiResponse("Successfully retrieved tenant id", tenantId);
            }
            throw new ApiException("The API key does not have a valid prefix", StatusCodes.Status400BadRequest);
        }

        ///<summary>
        /// Gets the project for a project's name
        ///</summary>
        ///<param name="tenantId">The tenant id the user is associated with</param>
        ///<param name="projectName">The name of the project to query for</param>
        ///<param name="email">The email to retrieve the authorized projects for</param>
        ///<param name="apiKey">The apiKey of the project to retrieve</param>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("/projects/{tenantId}")]
        public async Task<ApiResponse> GetProjects(
            string tenantId,
            [FromQuery] string projectName,
            [FromQuery] string email,
            [FromQuery] string apiKey)
        {
            if ((string.IsNullOrWhiteSpace(projectName) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(apiKey)) || (!string.IsNullOrWhiteSpace(projectName) && !string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(apiKey)))
            {
                var projects = await projectsService.GetAllProjects(tenantId);
                return new ApiResponse("Successfully retrieved projects", projects);
            }
            try
            {
                if (!string.IsNullOrWhiteSpace(projectName))
                {
                    var project = await projectsService.GetProjectByName(tenantId, projectName);
                    return new ApiResponse("Successfully retrieved projects", project);
                }
                else if (!string.IsNullOrWhiteSpace(email))
                {
                    var projects = await projectsService.GetProjectsForUser(tenantId, email);
                    return new ApiResponse("Successfully retrieved projects", projects);
                }
                else
                {
                    if (apiKey.StartsWith("live.") || apiKey.StartsWith("test."))
                    {
                        var project = await projectsService.GetProjectByApiKey(tenantId, apiKey);
                        if (project is null)
                        {
                            var message = $"No project was found for the provided API key.";
                            logger.LogWarning(message);
                            throw new ApiException(message, StatusCodes.Status404NotFound);
                        }
                        return new ApiResponse("Successfully retrieved projects", project);
                    }
                    throw new ApiException("The API key does not have a valid prefix", StatusCodes.Status400BadRequest);
                }
            }
            catch (Exception ex)
            {
                var message = "Failed to retrieve projects";
                logger.LogError(ex, message);
                throw new ApiException(message, StatusCodes.Status500InternalServerError);
            }
        }

        ///<summary>
        /// Resets the API key for a given environment
        ///</summary>
        ///<param name="tenantId">The tenant id the project API key is associated with</param>
        ///<param name="projectId">The project id for the API key to reset</param>
        ///<param name="environment">The environment for the API key to reset - "live" or "test"</param>
        ///<param name="apiKeyResetModel">The model necessary to verify the key reset</param>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [HttpPut("/projects/{tenantId}/{projectId}/{environment}/reset")]
        public async Task<ApiResponse> ApiKeyReset(string tenantId, Guid projectId, EnvironmentEnum environment, ApiKeyResetModel apiKeyResetModel)
        {
            var repo = projectsRepositoryFactory(tenantId);

            var environmentString = Enum.GetName(typeof(EnvironmentEnum), environment).ToLowerInvariant();
            try
            {
                var (project, newApiKey) = await repo.UpdateApiKeyAsync(apiKeyResetModel.UserEmail, environment, apiKeyResetModel.Version, projectId);
                return new ApiResponse("Successfully reset api key", new ProjectResponseModel
                {
                    ProjectId = project.ProjectId,
                    Name = project.Name,
                    Description = project.Description,
                    LiveApiKey = environmentString == "live" ? newApiKey : "",
                    TestApiKey = environmentString == "test" ? newApiKey : "",
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
                var _ = "Failed to reset the API key";
                logger.LogError(ex, _);
                throw new ApiException(_, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        ///<summary>
        /// Updates an existing project
        ///</summary>
        ///<param name="tenantId">The tenant id the project API key is associated with</param>
        ///<param name="projectId">The project id for the API key to reset</param>
        ///<param name="projectModel">The model necessary to update a project</param>
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
                throw new ApiException(String.IsNullOrWhiteSpace(ex.Message) ? "Failed to save the updated project" : ex.Message, StatusCodes.Status409Conflict);
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
                throw new ApiException(String.IsNullOrWhiteSpace(ex.Message) ? "Failed to save the updated project" : ex.Message, StatusCodes.Status400BadRequest);
            }
            catch (Exception ex)
            {
                var message = $"Failed to update project '{projectModel.Name}'";
                logger.LogError(ex, message);
                throw new ApiException(message, StatusCodes.Status500InternalServerError);
            }
            return new ApiResponse("Successfully updated project", new ProjectResponseModel
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
        /// Updates an existing project
        ///</summary>
        ///<param name="tenantId">The tenant id the project API key is associated with</param>
        ///<param name="projectId">The project id for the API key to reset</param>
        ///<param name="softDeleteModel">The model necessary to delete a project</param>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [HttpDelete("/projects/{tenantId}/{projectId}")]
        public async Task<ApiResponse> SoftDeleteProject(string tenantId, Guid projectId, SoftDeleteModel softDeleteModel)
        {
            var repo = projectsRepositoryFactory(tenantId);
            try
            {
                await repo.SoftDeleteAsync(softDeleteModel.UserEmail, projectId);
            }
            catch (ConcurrencyException ex)
            {
                logger.LogError(ex.Message);
                throw new ApiException(String.IsNullOrWhiteSpace(ex.Message) ? "Failed to save the updated project" : ex.Message, StatusCodes.Status409Conflict);
            }
            catch (RangerException ex)
            {
                logger.LogInformation(ex.Message);
                throw new ApiException(String.IsNullOrWhiteSpace(ex.Message) ? "Failed to save the updated project" : ex.Message, StatusCodes.Status400BadRequest);
            }
            catch (Exception ex)
            {
                var message = "Failed to delete project";
                logger.LogError(ex, message);
                throw new ApiException(message, StatusCodes.Status500InternalServerError);
            }
            return new ApiResponse("Successfully deleted project");
        }

        ///<summary>
        /// Creates a new project
        ///</summary>
        ///<param name="tenantId">The tenant id the project API key is associated with</param>
        ///<param name="projectModel">The model necessary to create a project</param>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [HttpPost("/projects/{tenantId}")]
        public async Task<ApiResponse> PostProject(string tenantId, PostProjectModel projectModel)
        {
            var limitsApiResponse = await subscriptionsClient.GetSubscription<SubscriptionLimitDetails>(tenantId);
            var repo = projectsRepositoryFactory(tenantId);
            var projects = await repo.GetAllProjects();
            if (!limitsApiResponse.Result.Active)
            {
                throw new ApiException($"Failed to create project '{projectModel.Name}'. Subscription is inactive", statusCode: StatusCodes.Status402PaymentRequired);
            }
            if (projects.Count() >= limitsApiResponse.Result.Limit.Projects)
            {
                throw new ApiException($"Failed to create project '{projectModel.Name}'. Subscription limit met", statusCode: StatusCodes.Status402PaymentRequired);
            }

            return await AddNewProject(tenantId, projectModel, repo);
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
                var message = $"Failed to create project '{projectModel.Name}'";
                logger.LogError(ex, message);
                throw new ApiException(message, StatusCodes.Status500InternalServerError);
            }

            return new ApiResponse("Successfully created new project", new ProjectResponseModel
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