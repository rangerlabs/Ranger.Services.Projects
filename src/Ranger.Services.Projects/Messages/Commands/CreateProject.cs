using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    [MessageNamespace("projects")]
    public class CreateProject : ICommand
    {
        public string TenantId { get; }
        public string Name { get; }
        public string Description { get; }
        public string UserEmail { get; }

        public CreateProject(string tenantId, string Name, string Description, string UserEmail)
        {
            if (string.IsNullOrWhiteSpace(UserEmail))
            {
                throw new System.ArgumentException($"{nameof(UserEmail)} was null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new System.ArgumentException($"{nameof(tenantId)} was null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new System.ArgumentException($"{nameof(Name)} was null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(Description))
            {
                throw new System.ArgumentException($"{nameof(Description)} was null or whitespace.");
            }
            this.TenantId = tenantId;
            this.Name = Name;
            this.Description = Description;
            this.UserEmail = UserEmail;
        }
    }
}