using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{

    public class ProjectsRepository : BaseRepository<ProjectsRepository>, IProjectsRepository
    {
        private readonly ContextTenant contextTenant;
        private readonly ProjectsDbContext.Factory context;
        private readonly CloudSqlOptions cloudSqlOptions;
        private readonly ILogger<BaseRepository<ProjectsRepository>> logger;

        public ProjectsRepository(ContextTenant contextTenant, ProjectsDbContext.Factory context, CloudSqlOptions cloudSqlOptions, ILogger<BaseRepository<ProjectsRepository>> logger) : base(contextTenant, context, cloudSqlOptions, logger)
        {
            this.contextTenant = contextTenant;
            this.context = context;
            this.cloudSqlOptions = cloudSqlOptions;
            this.logger = logger;
        }

        public async Task AddProjectAsync(Project project)
        {
            Context.Add(project);
            await Context.SaveChangesAsync();
        }

        public async Task<Project> GetProjectByNameAsync(string name)
        {
            return await Context.Projects.SingleOrDefaultAsync(p => p.Name == name);
        }

        public async Task<Project> GetProjectByApiKeyAsync(string apiKey)
        {
            Guid parsedApiKey;
            try
            {
                parsedApiKey = Guid.Parse(apiKey);
            }
            catch (Exception)
            {
                logger.LogError($"Failed to parse api key '{apiKey}' to a GUID.");
                throw;
            }
            return await Context.Projects.SingleOrDefaultAsync(p => p.ApiKey == parsedApiKey);
        }

        public async Task RemoveProjectAsync(string name)
        {
            var project = await GetProjectByNameAsync(name);
            Context.Remove(project);
            await Context.SaveChangesAsync();
        }

        public async Task UpdateProjectAsync(Project project)
        {
            Context.Update(project);
            await Context.SaveChangesAsync();
        }
    }
}