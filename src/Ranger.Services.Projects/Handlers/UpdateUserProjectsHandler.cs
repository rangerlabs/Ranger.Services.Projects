using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ranger.Common;
using Ranger.InternalHttpClient;
using Ranger.RabbitMQ;
using Ranger.RabbitMQ.BusPublisher;
using Ranger.Services.Projects.Data;

namespace Ranger.Services.Projects
{
    public class UpdateUserProjectsHandler : ICommandHandler<UpdateUserProjects>
    {
        private readonly IBusPublisher busPublisher;
        private readonly Func<string, ProjectUsersRepository> projectUsersRepositoryFactory;
        private readonly ILogger<UpdateUserProjectsHandler> logger;

        public UpdateUserProjectsHandler(IBusPublisher busPublisher, Func<string, ProjectUsersRepository> projectUsersRepositoryFactory, ILogger<UpdateUserProjectsHandler> logger)
        {
            this.busPublisher = busPublisher;
            this.projectUsersRepositoryFactory = projectUsersRepositoryFactory;
            this.logger = logger;
        }

        //TODO: Currently there is no check of whether a user is authorized to alter the the projects,
        // but we should only arrive here through the saga where we have verified whether a user can alter the role or not,
        // still not enough but enough for now
        public async Task HandleAsync(UpdateUserProjects command, ICorrelationContext context)
        {
            var projectUsersRepository = projectUsersRepositoryFactory(command.TenantId);
            var currentlyAuthorizedProjectIds = await projectUsersRepository.GetAuthorizedProjectIdsForUserEmail(command.Email);

            var distinctNewProjectIds = command.ProjectIds.Distinct();
            var alreadyAuthorizedProjects = currentlyAuthorizedProjectIds.Intersect(distinctNewProjectIds);

            var projectsToAdd = distinctNewProjectIds.Except(alreadyAuthorizedProjects);
            var projectsToRemove = currentlyAuthorizedProjectIds.Except(alreadyAuthorizedProjects);

            IEnumerable<Guid> failedAdds = null;
            IEnumerable<Guid> failedRemoves = null;
            if (projectsToAdd.Count() > 0)
            {
                failedAdds = await projectUsersRepository.AddUserToProjects(command.UserId, command.Email, projectsToAdd, command.CommandingUserEmail);
            }
            if (projectsToRemove.Count() > 0)
            {
                failedRemoves = await projectUsersRepository.RemoveUserFromProjects(command.UserId, projectsToRemove, command.CommandingUserEmail);
            }
            busPublisher.Publish(new UserProjectsUpdated(command.UserId, command.Email, failedAdds ?? new List<Guid>(), failedRemoves ?? new List<Guid>()), context);
        }
    }
}