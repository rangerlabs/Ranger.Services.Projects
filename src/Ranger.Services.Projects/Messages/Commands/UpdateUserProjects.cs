using System;
using System.Collections.Generic;
using System.Linq;
using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    [MessageNamespace("projects")]
    public class UpdateUserProjects : ICommand
    {
        public string TenantId { get; }
        public string Email { get; }
        public string CommandingUserEmail { get; }
        public string UserId { get; }
        public readonly IEnumerable<Guid> ProjectIds;

        public UpdateUserProjects(string tenantId, IEnumerable<Guid> projectIds, string userId, string email, string commandingUserEmail)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentException($"{nameof(tenantId)} was null or whitespace");
            }
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException($"{nameof(userId)} was null or whitespace");
            }
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException($"{nameof(email)} was null or whitespace");
            }
            if (projectIds is null)
            {
                throw new ArgumentException($"{nameof(projectIds)} was null");
            }
            if (string.IsNullOrWhiteSpace(commandingUserEmail))
            {
                throw new ArgumentException($"{nameof(commandingUserEmail)} was null or whitespace");
            }
            this.TenantId = tenantId;
            this.ProjectIds = projectIds;
            this.UserId = userId;
            this.Email = email;
            this.CommandingUserEmail = commandingUserEmail;
        }
    }
}