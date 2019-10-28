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
        private readonly IBusPublisher busPublisher;
        private readonly ProjectsRepository.Factory projectsRepositoryFactory;
        private readonly ILogger<ProjectController> logger;

        public ProjectController(IBusPublisher busPublisher, ITenantsClient tenantsClient, ProjectsRepository.Factory projectsRepositoryFactory, ILogger<ProjectController> logger)
        {
            this.busPublisher = busPublisher;
            this.tenantsClient = tenantsClient;
            this.projectsRepositoryFactory = projectsRepositoryFactory;
            this.logger = logger;
        }

        [HttpGet("{domain}/project")]
        public async Task<IActionResult> GetProjectByApiKey([FromRoute] string domain, [FromQuery]string apiKey)
        {
            ContextTenant tenant = null;
            try
            {
                tenant = await this.tenantsClient.GetTenantAsync<ContextTenant>(domain);
            }
            catch (HttpClientException ex)
            {
                if ((int)ex.ApiResponse.StatusCode == StatusCodes.Status404NotFound)
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "An exception occurred retrieving the ContextTenant object. Cannot construct the tenant specific repository.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var repo = projectsRepositoryFactory.Invoke(tenant);
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

        [HttpGet("{domain}/project/all")]
        public async Task<IActionResult> GetProjects([FromRoute]string domain)
        {
            ContextTenant tenant = null;
            try
            {
                tenant = await this.tenantsClient.GetTenantAsync<ContextTenant>(domain);
            }
            catch (HttpClientException ex)
            {
                if ((int)ex.ApiResponse.StatusCode == StatusCodes.Status404NotFound)
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "An exception occurred retrieving the ContextTenant object. Cannot construct the tenant specific repository.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var repo = projectsRepositoryFactory.Invoke(tenant);
            IEnumerable<(Project project, int version)> projects = await repo.GetAllProjects();
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
        public async Task<IActionResult> ApiKeyReset([FromRoute] string domain, [FromRoute] string projectId, [FromRoute] string environment, ApiKeyResetModel apiKeyResetModel)
        {
            Guid projectIdGuid;
            if (string.IsNullOrWhiteSpace(projectId) || !Guid.TryParse(projectId, out projectIdGuid))
            {
                var invalidProjectIdErrors = new ApiErrorContent();
                invalidProjectIdErrors.Errors.Add("Invalid project id format.");
                return BadRequest(invalidProjectIdErrors);
            }
            if (environment == "live" || environment == "test")
            {
                ContextTenant tenant = null;
                try
                {
                    tenant = await this.tenantsClient.GetTenantAsync<ContextTenant>(domain);
                }
                catch (HttpClientException ex)
                {
                    if ((int)ex.ApiResponse.StatusCode == StatusCodes.Status404NotFound)
                    {
                        var noTenantFoundErrors = new ApiErrorContent();
                        noTenantFoundErrors.Errors.Add($"No tenant was foud for domain '{domain}'.");
                        return NotFound(noTenantFoundErrors);
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "An exception occurred retrieving the ContextTenant object. Cannot construct the tenant specific repository.");
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }

                var repo = projectsRepositoryFactory.Invoke(tenant);
                try
                {
                    var (project, newApiKey) = await repo.UpdateApiKeyAsync(apiKeyResetModel.UserEmail, environment, apiKeyResetModel.Version, projectId);
                    return Ok(new ProjectResponseModel
                    {
                        ProjectId = project.ProjectId.ToString(),
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
        public async Task<IActionResult> PutProject([FromRoute] string domain, [FromRoute] string projectId, PutProjectModel projectModel)
        {
            Guid projectIdGuid;
            if (string.IsNullOrWhiteSpace(projectId) || !Guid.TryParse(projectId, out projectIdGuid))
            {
                var invalidProjectIdErrors = new ApiErrorContent();
                invalidProjectIdErrors.Errors.Add("Invalid project id format.");
                return BadRequest(invalidProjectIdErrors);
            }

            ContextTenant tenant = null;
            try
            {
                tenant = await this.tenantsClient.GetTenantAsync<ContextTenant>(domain);
            }
            catch (HttpClientException ex)
            {
                if ((int)ex.ApiResponse.StatusCode == StatusCodes.Status404NotFound)
                {
                    var errors = new ApiErrorContent();
                    errors.Errors.Add($"No tenant was foud for domain '{domain}'.");
                    return NotFound(errors);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "An exception occurred retrieving the ContextTenant object. Cannot construct the tenant specific repository.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var repo = projectsRepositoryFactory.Invoke(tenant);
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
                updatedProject = await repo.UpdateProjectAsync
                (
                    projectModel.UserEmail,
                    "ProjectUpdated",
                    projectModel.Version,
                    new Project
                    {
                        ProjectId = projectIdGuid,
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
                ProjectId = updatedProject.ProjectId.ToString(),
                Name = updatedProject.Name,
                Description = updatedProject.Description,
                Enabled = updatedProject.Enabled,
                Version = projectModel.Version,
                LiveApiKeyPrefix = updatedProject.LiveApiKeyPrefix,
                TestApiKeyPrefix = updatedProject.TestApiKeyPrefix
            });
        }

        [HttpDelete("{domain}/project/{projectId}")]
        public async Task<IActionResult> SoftDeleteProject([FromRoute]string domain, [FromRoute]string projectId, [FromBody]PutProjectModel projectModel)
        {
            ContextTenant tenant = null;
            try
            {
                tenant = await this.tenantsClient.GetTenantAsync<ContextTenant>(domain);
            }
            catch (HttpClientException ex)
            {
                if ((int)ex.ApiResponse.StatusCode == StatusCodes.Status404NotFound)
                {
                    return NotFound(ex.ApiResponse.Errors);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "An exception occurred retrieving the ContextTenant object. Cannot construct the tenant specific repository.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var repo = projectsRepositoryFactory.Invoke(tenant);
            try
            {
                await repo.SoftDeleteAsync(projectModel.UserEmail, projectId);
                return NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete project stream.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }


        [HttpPost("{domain}/project")]
        public async Task<IActionResult> PostProject([FromRoute]string domain, PostProjectModel projectModel)
        {
            ContextTenant tenant = null;
            try
            {
                tenant = await this.tenantsClient.GetTenantAsync<ContextTenant>(domain);
            }
            catch (HttpClientException ex)
            {
                if ((int)ex.ApiResponse.StatusCode == StatusCodes.Status404NotFound)
                {
                    return NotFound(ex.ApiResponse.Errors);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "An exception occurred retrieving the ContextTenant object. Cannot construct the tenant specific repository.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var repo = projectsRepositoryFactory.Invoke(tenant);
            return await AddNewProject(domain, projectModel, repo);
        }

        private async Task<IActionResult> AddNewProject(string domain, PostProjectModel projectModel, ProjectsRepository repo)
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
                await repo.AddProjectAsync(domain, projectModel.UserEmail, "ProjectCreated", project);
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
                ProjectId = project.ProjectId.ToString(),
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
