using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Ranger.Common;
using Ranger.InternalHttpClient;
using Ranger.RabbitMQ;
using Ranger.Services.Projects.Data;

namespace Ranger.Services.Projects
{
    public class CreateProjectHandler : ICommandHandler<CreateProject>
    {
        private readonly ITenantsClient tenantsClient;
        private readonly IBusPublisher busPublisher;
        private readonly ProjectsRepository.Factory projectsRepositoryFactory;
        private readonly ILogger<CreateProjectHandler> logger;

        public CreateProjectHandler(IBusPublisher busPublisher, ITenantsClient tenantsClient, ProjectsRepository.Factory projectsRepositoryFactory, ILogger<CreateProjectHandler> logger)
        {
            this.busPublisher = busPublisher;
            this.tenantsClient = tenantsClient;
            this.projectsRepositoryFactory = projectsRepositoryFactory;
            this.logger = logger;
        }

        public async Task HandleAsync(CreateProject command, ICorrelationContext context)
        {
            ContextTenant tenant = null;
            try
            {
                tenant = await this.tenantsClient.GetTenantAsync<ContextTenant>(command.Domain);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "An exception occurred retrieving the ContextTenant object. Cannot construct the tenant specific repository.");
                throw;
            }

            var repo = projectsRepositoryFactory.Invoke(tenant);
            var project = new Project
            {
                Name = command.Name,
                Description = command.Description,
                ApiKey = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = command.UserEmail
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
                    throw new RangerException($"Project Name '${command.Name}' is associated with an existing project for domain '{command.Domain}'.");
                }
            }
            busPublisher.Publish(new ProjectCreated(command.Domain, project.Name, project.Description, project.ApiKey.ToString()), context);
        }
    }
}