using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ranger.RabbitMQ;
using Ranger.RabbitMQ.BusPublisher;
using Ranger.Services.Projects.Data;

namespace Ranger.Services.Projects.Handlers
{
    public class EnforceProjectResourceLimitsHandler : ICommandHandler<EnforceProjectResourceLimits>
    {
        private readonly IBusPublisher busPublisher;
        private readonly Func<string, ProjectsRepository> projectsRepositoryFactory;
        private readonly ProjectsService _projectsService;
        private readonly ILogger<EnforceProjectResourceLimitsHandler> logger;

        public EnforceProjectResourceLimitsHandler(IBusPublisher busPublisher, Func<string, ProjectsRepository> projectUsersRepositoryFactory, ProjectsService projectsService, ILogger<EnforceProjectResourceLimitsHandler> logger)
        {
            this.busPublisher = busPublisher;
            this.projectsRepositoryFactory = projectUsersRepositoryFactory;
            this._projectsService = projectsService;
            this.logger = logger;
        }

        public async Task HandleAsync(EnforceProjectResourceLimits message, ICorrelationContext context)
        {
            var tenantRemainingProjects = new List<(string, IEnumerable<Guid>)>();
            foreach (var tenantLimit in message.TenantLimits)
            {
                var repo = projectsRepositoryFactory(tenantLimit.Item1);
                var projects = await repo.GetAllNotDeletedProjects();
                if (projects.Count() > tenantLimit.Item2)
                {
                    var exceededByCount = projects.Count() - tenantLimit.Item2;
                    var projectsToRemove = projects.OrderByDescending(p => p.project.CreatedOn).Take(exceededByCount);
                    foreach (var projectToRemove in projectsToRemove)
                    {
                        await repo.SoftDeleteAsync("SubscriptionEnforcer", projectToRemove.project.Id);
                        var tasks = new Task[3]
                        {
                            _projectsService.RemoveTenantIdFromRedisByHashedApiKey(projectToRemove.project.HashedLiveApiKey),
                            _projectsService.RemoveTenantIdFromRedisByHashedApiKey(projectToRemove.project.HashedTestApiKey),
                            _projectsService.RemoveTenantIdFromRedisByHashedApiKey(projectToRemove.project.HashedProjectApiKey)
                        };
                        await Task.WhenAll(tasks);
                    }
                    var remainingProjects = projects.Select(p => p.project.Id).Except(projectsToRemove.Select(p => p.project.Id));
                    tenantRemainingProjects.Add((tenantLimit.Item1, remainingProjects));
                }
                else
                {
                    tenantRemainingProjects.Add((tenantLimit.Item1, projects.Select(p => p.project.Id)));
                }
            }
            busPublisher.Publish(new ProjectResourceLimitsEnforced(tenantRemainingProjects), context);
        }
    }
}