using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Ranger.Common;
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
                    return NotFound(new { error = $"No tenant was foud for domain '{domain}'." });
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "An exception occurred retrieving the ContextTenant object. Cannot construct the tenant specific repository.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var repo = projectsRepositoryFactory.Invoke(tenant);
            return Ok(await repo.GetAllProjects());
        }

        [HttpPut("{domain}/project/{projectId}")]
        public async Task<IActionResult> PutProject([FromRoute] string domain, [FromRoute] string projectId, PutProjectModel projectModel)
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
                    return NotFound(new { error = $"No tenant was foud for domain '{domain}'." });
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "An exception occurred retrieving the ContextTenant object. Cannot construct the tenant specific repository.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var repo = projectsRepositoryFactory.Invoke(tenant);
            var project = await repo.GetProjectByProjectIdAsync(domain, projectId);
            if (project is null)
            {
                return NotFound();
            }
            var updatedProject = new Project
            {
                Name = projectModel.Name,
                Description = projectModel.Description,
                Enabled = projectModel.Enabled,
                Version = projectModel.Version
            };

            try
            {
                await repo.UpdateProjectAsync(domain, User.UserFromClaims().Email, "ProjectUpdated", updatedProject);
            }
            catch (ConcurrencyException ex)
            {
                logger.LogError(ex.Message);
                return Conflict(new { error = ex.Message });
            }
            catch (NoOpException ex)
            {
                logger.LogInformation(ex.Message);
                return StatusCode(StatusCodes.Status304NotModified);
            }
            return Ok(new ProjectResponseModel(domain, project.ProjectId, project.Name, project.Description, project.ApiKey.ToString(), project.Enabled));
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
                    return NotFound(new { error = $"No tenant was foud for domain '{domain}'." });
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
                Version = 0,
                ProjectId = Guid.NewGuid(),
                Name = projectModel.Name,
                ApiKey = Guid.NewGuid(),
                Enabled = projectModel.Enabled,
                Description = projectModel.Description
            };
            try
            {
                await repo.AddProjectAsync(domain, User.UserFromClaims().Email, "ProjectCreated", project);
            }
            catch (DbUpdateException ex)
            {
                var postgresException = ex.InnerException as PostgresException;
                if (postgresException.SqlState == "23505")
                {
                    return Conflict(new { error = $"Project Name '${projectModel.Name}' is associated with an existing project for domain '{domain}'." });
                }
            }
            return Ok(new ProjectResponseModel(domain, project.ProjectId, project.Name, project.Description, project.ApiKey.ToString(), project.Enabled));
        }
    }
}
