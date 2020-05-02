using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ranger.RabbitMQ;
using Ranger.Services.Projects.Data;

namespace Ranger.Services.Projects.Handlers
{
    public class EnforceProjectResourceLimitsHandler : ICommandHandler<EnforceProjectResourceLimits>
    {
        private readonly Func<string, ProjectsRepository> projectsRepositoryFactory;
        private readonly ILogger<EnforceProjectResourceLimitsHandler> logger;

        public EnforceProjectResourceLimitsHandler(Func<string, ProjectsRepository> projectUsersRepositoryFactory, ILogger<EnforceProjectResourceLimitsHandler> logger)
        {
            this.projectsRepositoryFactory = projectUsersRepositoryFactory;
            this.logger = logger;
        }

        public async Task HandleAsync(EnforceProjectResourceLimits message, ICorrelationContext context)
        {
            var repo = projectsRepositoryFactory(message.TenantId);
            var projects = await repo.GetAllProjects();
            if (projects.Count() > message.Limit)
            {
                var exceededByCount = projects.Count() - message.Limit;
                var projectsToRemove = projects.OrderByDescending(p => p.project.CreatedOn).Take(exceededByCount);
                foreach (var projectToRemove in projectsToRemove)
                {
                    await repo.SoftDeleteAsync("SubscriptionEnforcer", projectToRemove.project.ProjectId);
                }
            }
        }
    }
}