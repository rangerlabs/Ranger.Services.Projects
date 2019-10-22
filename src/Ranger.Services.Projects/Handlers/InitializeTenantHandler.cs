using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ranger.Common;
using Ranger.RabbitMQ;
using Ranger.Services.Projects.Data;

namespace Ranger.Services.Projects
{
    public class InitializeTenantHandler : ICommandHandler<InitializeTenant>
    {
        private readonly IBusPublisher busPublisher;
        private readonly ILoginRoleRepository<ProjectsDbContext> loginRoleRepository;
        private readonly ProjectsDbContext identityDbContext;
        private readonly ILogger<InitializeTenantHandler> logger;

        public InitializeTenantHandler(IBusPublisher busPublisher, ILoginRoleRepository<ProjectsDbContext> loginRoleRepository, ProjectsDbContext identityDbContext, ILogger<InitializeTenantHandler> logger)
        {
            this.busPublisher = busPublisher;
            this.loginRoleRepository = loginRoleRepository;
            this.identityDbContext = identityDbContext;
            this.logger = logger;
        }

        public async Task HandleAsync(InitializeTenant command, ICorrelationContext context)
        {
            await this.loginRoleRepository.CreateTenantLoginRole(command.DatabaseUsername, command.DatabasePassword);
            logger.LogInformation($"New tenant login '{command.DatabaseUsername}' added to Projects database.");

            var tables = Enum.GetNames(typeof(RowLevelSecureTablesEnum)).Concat(Enum.GetNames(typeof(PublicTablesEnum)));
            foreach (var table in tables)
            {
                logger.LogInformation($"Setting tenant login permissions on table: '{table}'.");
                await this.loginRoleRepository.GrantTenantLoginRoleTablePermissions(command.DatabaseUsername, table);
            }
            logger.LogInformation("Setting tenant login sequence permissions");
            await this.loginRoleRepository.GrantTenantLoginRoleSequencePermissions(command.DatabaseUsername);

            logger.LogInformation($"New Projects tenant initialized successfully.");
            busPublisher.Publish(new TenantInitialized(), context);
        }
    }
}