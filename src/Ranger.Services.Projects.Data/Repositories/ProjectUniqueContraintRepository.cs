using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{
    public class ProjectUniqueContraintRepository : IProjectUniqueContraintRepository
    {
        private readonly ProjectsDbContext context;
        public ProjectUniqueContraintRepository(ProjectsDbContext context)
        {
            this.context = context;
        }

        public async Task<string> GetDatabaseUsernameByApiKeyAsync(Guid apiKey)
        {
            var databaseUserName = await context.ProjectUniqueConstraints.Where(_ => _.ApiKey == apiKey).Select(_ => _.DatabaseUsername).SingleOrDefaultAsync();
            return databaseUserName;
        }

        public async Task<bool> GetProjectNameAvailableByDomainAsync(string domain, string name)
        {
            return await context.ProjectUniqueConstraints.AnyAsync(_ => _.Name == name);
        }

    }
}