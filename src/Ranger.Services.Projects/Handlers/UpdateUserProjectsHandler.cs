using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ranger.Common;
using Ranger.InternalHttpClient;
using Ranger.RabbitMQ;
using Ranger.Services.Projects.Data;

namespace Ranger.Services.Projects
{
    public class UpdateUserProjectsHandler : ICommandHandler<UpdateUserProjects>
    {
        private readonly IBusPublisher busPublisher;
        private readonly ProjectUsersRepository.Factory projectUsersRepositoryFactory;
        private readonly ILogger<UpdateUserProjectsHandler> logger;
        private readonly ITenantsClient tenantsClient;

        public UpdateUserProjectsHandler(IBusPublisher busPublisher, ITenantsClient tenantsClient, ProjectUsersRepository.Factory projectUsersRepositoryFactory, ILogger<UpdateUserProjectsHandler> logger)
        {
            this.tenantsClient = tenantsClient;
            this.busPublisher = busPublisher;
            this.projectUsersRepositoryFactory = projectUsersRepositoryFactory;
            this.logger = logger;
        }

        //TODO: Currently there is NonSerializedAttribute check of whether a user is authorized to alter the the projects,
        // but we should only arrive here through the saga where we have verified whether a user can alter the role or not,
        // still not enough but enough for now
        public async Task HandleAsync(UpdateUserProjects command, ICorrelationContext context)
        {
            ContextTenant tenant = null;
            try
            {
                tenant = await this.tenantsClient.GetTenantAsync<ContextTenant>(command.Domain);
            }
            catch (HttpClientException ex)
            {
                if ((int)ex.ApiResponse.StatusCode == StatusCodes.Status404NotFound)
                {
                    throw new RangerException($"To tenant found for domain {command.Domain}.");
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "An exception occurred retrieving the ContextTenant object. Cannot construct the tenant specific repository.");
                throw;
            }

            var projectUsersRepository = projectUsersRepositoryFactory.Invoke(tenant);
            var authorizedProjectIds = await projectUsersRepository.GetAuthorizedProjectIdsForUserEmail(command.Email);

            var distinctNewProjectIds = authorizedProjectIds.Distinct();
            var alreadyAuthorizedProjects = distinctNewProjectIds.Intersect(authorizedProjectIds);

            var projectsToAdd = alreadyAuthorizedProjects.Except(distinctNewProjectIds);
            var projectsToRemove = alreadyAuthorizedProjects.Except(distinctNewProjectIds).Except(alreadyAuthorizedProjects);

            var failedAdds = await projectUsersRepository.AddUserToProjects(command.UserId, command.Email, projectsToAdd, command.CommandingUserEmail);
            var failedRemoves = await projectUsersRepository.RemoveUserFromProjects(command.UserId, projectsToRemove, command.CommandingUserEmail);
            busPublisher.Publish(new UserProjectsUpdated(command.UserId, command.Email, failedAdds, failedRemoves), context);
        }
    }
}