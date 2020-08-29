using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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

        public async Task<string> GetTenantIdByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            var hashedApiKey = Crypto.GenerateSHA512Hash(apiKey);
            if (apiKey.StartsWith("live."))
            {
                return await context.ProjectUniqueConstraints.Where(_ => _.HashedLiveApiKey == hashedApiKey).Select(_ => _.TenantId).SingleOrDefaultAsync(cancellationToken);
            }
            else if (apiKey.StartsWith("test."))
            {
                return await context.ProjectUniqueConstraints.Where(_ => _.HashedTestApiKey == hashedApiKey).Select(_ => _.TenantId).SingleOrDefaultAsync(cancellationToken);
            }
            else if (apiKey.StartsWith("proj."))
            {
                return await context.ProjectUniqueConstraints.Where(_ => _.HashedProjectApiKey == hashedApiKey).Select(_ => _.TenantId).SingleOrDefaultAsync(cancellationToken);
            }
            else
            {
                return null;
            }
        }

        public async Task<bool> GetProjectNameAvailableByDomainAsync(string domain, string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await context.ProjectUniqueConstraints.AnyAsync(_ => _.Name == name, cancellationToken);
        }

    }
}