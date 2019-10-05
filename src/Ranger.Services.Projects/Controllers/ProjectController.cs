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
        private readonly ILogger<CreateProjectHandler> logger;

        public ProjectController(IBusPublisher busPublisher, ITenantsClient tenantsClient, ProjectsRepository.Factory projectsRepositoryFactory, ILogger<CreateProjectHandler> logger)
        {
            this.busPublisher = busPublisher;
            this.tenantsClient = tenantsClient;
            this.projectsRepositoryFactory = projectsRepositoryFactory;
            this.logger = logger;
        }
        [HttpPost("{domain}/project")]
        public async Task<IActionResult> PostProject([FromRoute]string domain, ProjectModel projectModel)
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
                    return NotFound(new { error = $"No tenant was foud for domain '{projectModel.Domain}'." });
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "An exception occurred retrieving the ContextTenant object. Cannot construct the tenant specific repository.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var repo = projectsRepositoryFactory.Invoke(tenant);
            var project = new Project
            {
                Name = projectModel.Name,
                Description = projectModel.Description,
                ApiKey = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = projectModel.UserEmail
            };
            try
            {
                await repo.AddProjectAsync(project);
            }
            catch (DbUpdateException ex)
            {
                var postgresException = ex.InnerException as PostgresException;
                if (postgresException.SqlState == "23505")
                {
                    return Conflict(new { error = $"Project Name '${projectModel.Name}' is associated with an existing project for domain '{projectModel.Domain}'." });
                }
            }
            return Ok(new ProjectCreated(projectModel.Domain, project.Name, project.Description, project.ApiKey.ToString()));
        }
    }
}
