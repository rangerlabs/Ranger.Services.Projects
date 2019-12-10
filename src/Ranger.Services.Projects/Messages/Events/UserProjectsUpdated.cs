using System;
using System.Collections.Generic;
using System.Linq;
using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    [MessageNamespace("projects")]
    public class UserProjectsUpdated : IEvent
    {
        public string Email { get; }
        public string UserId { get; }
        public IEnumerable<string> UnSuccessfullyAddedProjectIds { get; }
        public IEnumerable<string> UnSuccessfullyRemovedProjectIds { get; }

        public UserProjectsUpdated(string userId, string email, IEnumerable<string> unSuccessfullyAddedProjectIds = null, IEnumerable<string> unSuccessfullyRemovedProjectIds = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException($"{nameof(userId)} was null or whitespace.");
            }
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException($"{nameof(email)} was null or whitespace.");
            }

            this.UserId = userId;
            this.Email = email;
            this.UnSuccessfullyAddedProjectIds = unSuccessfullyAddedProjectIds is null ? new List<string>() : unSuccessfullyAddedProjectIds;
            this.UnSuccessfullyRemovedProjectIds = unSuccessfullyRemovedProjectIds is null ? new List<string>() : unSuccessfullyRemovedProjectIds;
        }
    }
}