using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Npgsql;
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

        public async Task AddProjectAsync(string domain, string userEmail, string eventName, Project project)
        {
            var serializedNewProjectData = JsonConvert.SerializeObject(project);
            var newProjectStream = new ProjectStream<Project>()
            {
                StreamId = Guid.NewGuid(),
                Domain = domain,
                ProjectId = project.ProjectId,
                ApiKey = project.ApiKey,
                Version = project.Version,
                Data = serializedNewProjectData,
                Event = eventName,
                InsertedAt = DateTime.UtcNow,
                InsertedBy = userEmail,
            };
            Context.ProjectStreams.Add(newProjectStream);
            await Context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Project>> GetAllProjects()
        {
            var projectStreams = await Context.ProjectStreams.GroupBy(ps => ps.StreamId).Select(group => group.OrderByDescending(ps => ps.Version).First()).ToListAsync();
            List<Project> projects = new List<Project>();
            foreach (var projectStream in projectStreams)
            {
                projects.Add(JsonConvert.DeserializeObject<Project>(projectStream.Data));
            }
            return projects;
        }

        public async Task<Project> GetProjectByProjectIdAsync(string domain, string projectId)
        {
            Guid parsedProjectId = ParseGuid(projectId);
            var projectStream = await Context.ProjectStreams.Where(p => p.Domain == domain && p.ProjectId == parsedProjectId).OrderByDescending(ps => ps.Version).FirstOrDefaultAsync();
            return JsonConvert.DeserializeObject<Project>(projectStream.Data);
        }

        public async Task<Project> GetProjectByApiKeyAsync(string apiKey)
        {
            Guid parsedApiKey = ParseGuid(apiKey);
            var projectStream = await Context.ProjectStreams.Where(p => p.ApiKey == parsedApiKey).OrderByDescending(ps => ps.Version).FirstOrDefaultAsync();
            return JsonConvert.DeserializeObject<Project>(projectStream.Data);
        }

        private Guid ParseGuid(string apiKey)
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

            return parsedApiKey;
        }

        public async Task RemoveProjectAsync(string domain, string projectId)
        {
            Guid parsedProjectId = ParseGuid(projectId);
            Context.RemoveRange(Context.ProjectStreams.Where(ps => ps.Domain == domain && ps.ProjectId == parsedProjectId));
            await Context.SaveChangesAsync();
        }

        private async Task<ProjectStream<Project>> GetProjectStreamByProjectIdAsync(string domain, Guid projectId)
        {
            return await Context.ProjectStreams.Where(ps => ps.Domain == domain && ps.ProjectId == projectId).FirstOrDefaultAsync();
        }

        public async Task UpdateProjectAsync(string domain, string userEmail, string eventName, Project project)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException($"{nameof(domain)} was null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(userEmail))
            {
                throw new ArgumentException($"{nameof(userEmail)} was null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException($"{nameof(eventName)} was null or whitespace.");
            }

            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            var currentProjectStream = await GetProjectStreamByProjectIdAsync(domain, project.ProjectId);
            if (project.Version - currentProjectStream.Version > 1)
            {
                throw new ConcurrencyException($"The update version number was too high. The current stream version is '{currentProjectStream.Version}' and the request update version was '{project.Version}'.");
            }
            if (project.Version - currentProjectStream.Version <= 0)
            {
                throw new ConcurrencyException($"The update version number was outdated. The current stream version is '{currentProjectStream.Version}' and the request update version was '{project.Version}'.");
            }

            var serializedNewProjectData = JsonConvert.SerializeObject(project);
            if (serializedNewProjectData == currentProjectStream.Data)
            {
                throw new NoOpException("No changes were made from the previous version.");
            }

            var projectStreamUpdate = new ProjectStream<Project>()
            {
                StreamId = currentProjectStream.StreamId,
                Domain = currentProjectStream.Domain,
                ProjectId = currentProjectStream.ProjectId,
                ApiKey = currentProjectStream.ApiKey,
                Version = currentProjectStream.Version++,
                Data = serializedNewProjectData,
                Event = eventName,
                InsertedAt = DateTime.UtcNow,
                InsertedBy = userEmail,
            };

            Context.ProjectStreams.Add(projectStreamUpdate);
            try
            {
                await Context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                var postgresException = ex.InnerException as PostgresException;
                if (postgresException.SqlState == "23505")
                {
                    throw new ConcurrencyException($"The update version number was outdated. The current stream version is '{currentProjectStream.Version}' and the request update version was '{project.Version}'.");
                }
            }
        }
    }
}