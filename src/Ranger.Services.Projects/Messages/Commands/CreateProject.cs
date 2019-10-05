using Ranger.RabbitMQ;

namespace Ranger.Services.Projects
{
    [MessageNamespace("projects")]
    public class CreateProject : ICommand
    {
        public string Name { get; }
        public string Description { get; }
        public string Domain { get; }
        public string UserEmail { get; }

        public CreateProject(string Domain, string Name, string Description, string UserEmail)
        {
            if (string.IsNullOrWhiteSpace(UserEmail))
            {
                throw new System.ArgumentException($"{nameof(UserEmail)} was null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(Domain))
            {
                throw new System.ArgumentException($"{nameof(Domain)} was null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new System.ArgumentException($"{nameof(Name)} was null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(Description))
            {
                throw new System.ArgumentException($"{nameof(Description)} was null or whitespace.");
            }
            this.Domain = Domain;
            this.Name = Name;
            this.Description = Description;
            this.UserEmail = UserEmail;
        }
    }
}