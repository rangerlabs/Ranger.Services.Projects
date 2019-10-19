using System;
using System.Collections.Generic;
using System.Linq;
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
                ApiKey = _.project.ApiKey,
                Description = _.project.Description,
                Enabled = _.project.Enabled,
                Name = _.project.Name,
                ProjectId = _.project.ProjectId,
                Version = _.version
            });
            return Ok(result);
        }

        [HttpPut("{domain}/project/{projectId}")]
        public async Task<IActionResult> PutProject([FromRoute] string domain, [FromRoute] string projectId, PutProjectModel projectModel)
        {
            Guid projectIdGuid;
            Guid apiKey;
            if (string.IsNullOrWhiteSpace(projectId) || !Guid.TryParse(projectId, out projectIdGuid) || !Guid.TryParse(projectModel.ApiKey, out apiKey))
            {
                throw new ArgumentException("Invalid project id format.");
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
            var updatedProject = new Project
            {
                ProjectId = projectIdGuid,
                Name = projectModel.Name,
                Description = projectModel.Description,
                ApiKey = apiKey,
                Enabled = projectModel.Enabled,
            };

            try
            {
                await repo.UpdateProjectAsync(domain, projectModel.UserEmail, "ProjectUpdated", projectModel.Version, updatedProject);
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
            return Ok(new ProjectResponseModel(updatedProject.ProjectId.ToString(), updatedProject.Name, updatedProject.Description, updatedProject.ApiKey.ToString(), updatedProject.Enabled, projectModel.Version));
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
            var project = new Project
            {
                ProjectId = Guid.NewGuid(),
                Name = projectModel.Name,
                ApiKey = Guid.NewGuid(),
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

            return Ok(new ProjectResponseModel(project.ProjectId.ToString(), project.Name, project.Description, project.ApiKey.ToString(), project.Enabled, 0));
        }
    }
}
