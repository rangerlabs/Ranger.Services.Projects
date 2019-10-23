using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using Ranger.Common;
using Ranger.Common.Data.Exceptions;

namespace Ranger.Services.Projects.Data
{

    //TODO: after updating to .net 3.0, use the new System.Text.Json API to query
    public class ProjectsRepository : BaseRepository<ProjectsRepository>, IProjectsRepository
    {
        private readonly ContextTenant contextTenant;
        private readonly ProjectsDbContext.Factory context;
        private readonly CloudSqlOptions cloudSqlOptions;
        private readonly ILogger<BaseRepository<ProjectsRepository>> logger;
        private readonly IProjectUniqueContraintRepository projectUniqueContraintRepository;

        public ProjectsRepository(ContextTenant contextTenant, ProjectsDbContext.Factory context, IProjectUniqueContraintRepository projectUniqueContraintRepository, CloudSqlOptions cloudSqlOptions, ILogger<BaseRepository<ProjectsRepository>> logger) : base(contextTenant, context, cloudSqlOptions, logger)
        {
            this.projectUniqueContraintRepository = projectUniqueContraintRepository;
            this.contextTenant = contextTenant;
            this.context = context;
            this.cloudSqlOptions = cloudSqlOptions;
            this.logger = logger;
        }

        public async Task AddProjectAsync(string domain, string userEmail, string eventName, Project project)
        {
            var serializedNewProjectData = JsonConvert.SerializeObject(project);

            var projectUniqueConstraint = this.AddProjectUniqueConstraints(project);
            var newProjectStream = new ProjectStream<Project>()
            {
                DatabaseUsername = this.contextTenant.DatabaseUsername,
                ProjectUniqueConstraint = projectUniqueConstraint,
                StreamId = Guid.NewGuid(),
                Version = 0,
                Data = serializedNewProjectData,
                Event = eventName,
                InsertedAt = DateTime.UtcNow,
                InsertedBy = userEmail,
            };
            Context.ProjectStreams.Add(newProjectStream);
            try
            {
                await Context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                var postgresException = ex.InnerException as PostgresException;
                if (postgresException.SqlState == "23505")
                {
                    var uniqueIndexViolation = postgresException.ConstraintName;
                    switch (uniqueIndexViolation)
                    {
                        case ProjectJsonbConstraintNames.Name:
                            {
                                throw new EventStreamDataConstraintException("The project name is in use by another project.");
                            }
                        default:
                            {
                                throw new EventStreamDataConstraintException("");
                            }
                    }
                }
                throw;
            }
        }

        public async Task<IEnumerable<(Project project, int version)>> GetAllProjects()
        {
            var projectStreams = await Context.ProjectStreams.GroupBy(ps => ps.StreamId).Select(group => group.OrderByDescending(ps => ps.Version).First()).ToListAsync();
            List<(Project project, int version)> projects = new List<(Project project, int version)>();
            foreach (var projectStream in projectStreams)
            {
                projects.Add((JsonConvert.DeserializeObject<Project>(projectStream.Data), projectStream.Version));
            }
            return projects;
        }

        public async Task<Project> GetProjectByProjectIdAsync(string projectId)
        {
            var projectStream = await Context.ProjectStreams.FromSql($"SELECT * FROM project_streams WHERE database_username = {contextTenant.DatabaseUsername} AND data ->> 'ProjectId' = {projectId} ORDER BY Version DESC").FirstOrDefaultAsync();
            return JsonConvert.DeserializeObject<Project>(projectStream.Data);
        }

        public async Task<Project> GetProjectByApiKeyAsync(Guid apiKey)
        {
            var projectStream = await Context.ProjectStreams.FromSql($"SELECT * FROM project_streams WHERE database_username = {contextTenant.DatabaseUsername} AND data ->> 'ApiKey' = {apiKey.ToString()} ORDER BY Version DESC").SingleAsync();
            return JsonConvert.DeserializeObject<Project>(projectStream.Data);
        }

        public async Task RemoveProjectAsync(string name)
        {
            Context.ProjectStreams.RemoveRange(
                await Context.ProjectStreams.FromSql($"SELECT * FROM project_streams WHERE database_username = {contextTenant.DatabaseUsername} AND data ->> 'Name' = {name} ORDER BY Version DESC").ToListAsync()
            );
        }

        public async Task UpdateProjectAsync(string domain, string userEmail, string eventName, int version, Project project)
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

            //TODO: This is a redundant call because we're retrieving the Project in the controller
            var currentProjectStream = await GetProjectStreamByProjectIdAsync(project.ProjectId.ToString());
            ValidateRequestVersionIncremented(version, currentProjectStream);

            var serializedNewProjectData = JsonConvert.SerializeObject(project);
            ValidateDataJsonInequality(currentProjectStream, serializedNewProjectData);

            var outdatedProjectStream = JsonConvert.DeserializeObject<Project>(currentProjectStream.Data);

            var uniqueConstraint = await this.GetProjectUniqueConstraintsByProjectIdAsync(project.ProjectId);
            if (project.Name != outdatedProjectStream.Name)
            {
                uniqueConstraint.Name = project.Name;
                Context.Update(uniqueConstraint);
            }

            var updatedProjectStream = new ProjectStream<Project>()
            {
                DatabaseUsername = this.contextTenant.DatabaseUsername,
                ProjectUniqueConstraint = uniqueConstraint,
                StreamId = currentProjectStream.StreamId,
                Version = version,
                Data = serializedNewProjectData,
                Event = eventName,
                InsertedAt = DateTime.UtcNow,
                InsertedBy = userEmail,
            };

            Context.ProjectStreams.Add(updatedProjectStream);
            try
            {
                await Context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                var postgresException = ex.InnerException as PostgresException;
                if (postgresException.SqlState == "23505")
                {
                    var uniqueIndexViolation = postgresException.ConstraintName;
                    switch (uniqueIndexViolation)
                    {
                        case ProjectJsonbConstraintNames.Name:
                            {
                                throw new EventStreamDataConstraintException("The project name is in use by another project.");
                            }
                        case ProjectJsonbConstraintNames.ProjectId_Version:
                            {
                                throw new ConcurrencyException($"The update version number was outdated. The current stream version is '{currentProjectStream.Version}' and the request update version was '{version}'.");
                            }
                        default:
                            {
                                throw new EventStreamDataConstraintException("");
                            }
                    }
                    throw;
                }
            }
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

        private async Task<ProjectStream<Project>> GetProjectStreamByProjectNameAsync(string name)
        {
            return await Context.ProjectStreams.FromSql($"SELECT * FROM project_streams WHERE database_username = {contextTenant.DatabaseUsername} AND data ->> 'Name' = {name} ORDER BY Version DESC").FirstOrDefaultAsync();
        }

        private static void ValidateDataJsonInequality(ProjectStream<Project> currentProjectStream, string serializedNewProjectData)
        {
            var currentJObject = JsonConvert.DeserializeObject<JObject>(currentProjectStream.Data);
            var requestJObject = JsonConvert.DeserializeObject<JObject>(serializedNewProjectData);
            if (JToken.DeepEquals(currentJObject, requestJObject))
            {
                throw new NoOpException("No changes were made from the previous version.");
            }
        }

        private static void ValidateRequestVersionIncremented(int version, ProjectStream<Project> currentProjectStream)
        {
            if (version - currentProjectStream.Version > 1)
            {
                throw new ConcurrencyException($"The update version number was too high. The current stream version is '{currentProjectStream.Version}' and the request update version was '{version}'.");
            }
            if (version - currentProjectStream.Version <= 0)
            {
                throw new ConcurrencyException($"The update version number was outdated. The current stream version is '{currentProjectStream.Version}' and the request update version was '{version}'.");
            }
        }

        private async Task<ProjectStream<Project>> GetProjectStreamByProjectIdAsync(string projectId)
        {
            return await Context.ProjectStreams.FromSql($"SELECT * FROM project_streams WHERE database_username = {contextTenant.DatabaseUsername} AND data ->> 'ProjectId' = {projectId} ORDER BY Version DESC").FirstOrDefaultAsync();
        }

        public async Task<ProjectUniqueConstraint> GetProjectUniqueConstraintsByProjectIdAsync(Guid projectId)
        {
            return await Context.ProjectUniqueConstraints.SingleOrDefaultAsync(_ => _.ProjectId == projectId);
        }

        private ProjectUniqueConstraint AddProjectUniqueConstraints(Project project)
        {
            var newProjectUniqueConstraint = new ProjectUniqueConstraint
            {
                ProjectId = project.ProjectId,
                DatabaseUsername = contextTenant.DatabaseUsername,
                Name = project.Name,
                ApiKey = project.ApiKey
            };
            Context.ProjectUniqueConstraints.Add(newProjectUniqueConstraint);
            return newProjectUniqueConstraint;
        }
    }
}