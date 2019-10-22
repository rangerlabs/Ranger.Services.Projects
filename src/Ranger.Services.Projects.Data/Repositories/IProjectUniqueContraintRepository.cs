using System;
using System.Threading.Tasks;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{
    public interface IProjectUniqueContraintRepository
    {
        Task<string> GetDatabaseUsernameByApiKeyAsync(Guid apiKey);
        Task<bool> GetProjectNameAvailableByDomainAsync(string domain, string name);
    }
}