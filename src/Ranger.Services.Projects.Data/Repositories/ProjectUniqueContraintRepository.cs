using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        public async Task<string> GetTenantIdByApiKeyAsync(string apiKey)
        {
            var hashedApiKey = Crypto.GenerateSHA512Hash(apiKey);
            if (apiKey.StartsWith("live."))
            {
                return await context.ProjectUniqueConstraints.Where(_ => _.HashedLiveApiKey == hashedApiKey).Select(_ => _.TenantId).SingleOrDefaultAsync();
            }
            else if (apiKey.StartsWith("test."))
            {
                return await context.ProjectUniqueConstraints.Where(_ => _.HashedTestApiKey == hashedApiKey).Select(_ => _.TenantId).SingleOrDefaultAsync();
            }
            else if (apiKey.StartsWith("proj."))
            {
                return await context.ProjectUniqueConstraints.Where(_ => _.HashedProjectApiKey == hashedApiKey).Select(_ => _.TenantId).SingleOrDefaultAsync();
            }
            else
            {
                return null;
            }
        }

        public async Task<bool> GetProjectNameAvailableByDomainAsync(string domain, string name)
        {
            return await context.ProjectUniqueConstraints.AnyAsync(_ => _.Name == name);
        }

    }
}