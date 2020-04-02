using System;
using Ranger.Common;
using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{

    [MessageNamespace("subscriptions")]
    public class DecrementResourceCount : ICommand
    {
        public string Domain { get; }
        public ResourceEnum Resource { get; }

        public DecrementResourceCount(string domain, ResourceEnum resource)
        {
            if (String.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException($"{nameof(domain)} was null or whitespace.");
            }
            this.Domain = domain;
            this.Resource = resource;
        }
    }
}