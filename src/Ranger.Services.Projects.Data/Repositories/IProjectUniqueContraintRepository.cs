using System;
using System.Threading;
using System.Threading.Tasks;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{
    public interface IProjectUniqueContraintRepository
    {
        Task<string> GetTenantIdByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default(CancellationToken));
        Task<bool> GetProjectNameAvailableByDomainAsync(string domain, string name, CancellationToken cancellationToken = default(CancellationToken));
    }
}