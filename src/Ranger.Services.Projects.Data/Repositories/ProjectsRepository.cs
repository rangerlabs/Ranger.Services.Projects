using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        public async Task AddProjectAsync(string userEmail, string eventName, Project project)
        {
            var serializedNewProjectData = JsonConvert.SerializeObject(project);

            var newProjectStream = new ProjectStream()
            {
                DatabaseUsername = this.contextTenant.DatabaseUsername,
                StreamId = Guid.NewGuid(),
                Version = 0,
                Data = serializedNewProjectData,
                Event = eventName,
                InsertedAt = DateTime.UtcNow,
                InsertedBy = userEmail,
            };
            var projectUniqueConstraint = this.AddProjectUniqueConstraints(newProjectStream, project);
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
            var projectStreams = await Context.ProjectStreams.
            FromSql(@"
                WITH not_deleted AS(
	                SELECT 
            	    	ps.id,
            	    	ps.database_username,
            	    	ps.stream_id,
            	    	ps.version,
            	    	ps.data,
            	    	ps.event,
            	    	ps.inserted_at,
            	    	ps.inserted_by
            	    FROM project_streams ps, project_unique_constraints puc
            	    WHERE (ps.data ->> 'Name') = puc.name
               )
               SELECT DISTINCT ON (ps.stream_id) 
              		ps.id,
              		ps.database_username,
              		ps.stream_id,
              		ps.version,
            		ps.data,
            		ps.event,
            		ps.inserted_at,
            		ps.inserted_by
                FROM not_deleted ps
                ORDER BY ps.stream_id, ps.version DESC;").ToListAsync();
            List<(Project project, int version)> projects = new List<(Project project, int version)>();
            foreach (var projectStream in projectStreams)
            {
                projects.Add((JsonConvert.DeserializeObject<Project>(projectStream.Data), projectStream.Version));
            }
            return projects;
        }

        public async Task<Project> GetProjectByProjectIdAsync(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
            {
                throw new ArgumentException($"{nameof(projectId)} was null or whitespace.");
            }
            var projectStream = await Context.ProjectStreams.FromSql($"SELECT * FROM project_streams WHERE data ->> 'ProjectId' = {projectId} AND data ->> 'Deleted' = 'false' ORDER BY version DESC").FirstOrDefaultAsync();
            return JsonConvert.DeserializeObject<Project>(projectStream.Data);
        }

        public async Task<string> GetProjectIdByCurrentNameAsync(string name)
        {
            return await Context.ProjectUniqueConstraints.Where(_ => _.Name == name).Select(_ => _.ProjectId.ToString()).SingleOrDefaultAsync();
        }

        public async Task<Project> GetProjectByApiKeyAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException($"{nameof(apiKey)} was null or whitespace.");
            }

            var hashedApiKey = Crypto.GenerateSHA512Hash(apiKey);

            ProjectStream projectStream = null;
            if (apiKey.StartsWith("live."))
            {
                projectStream = await Context.ProjectStreams.FromSql($"SELECT * FROM project_streams WHERE data ->> 'HashedLiveApiKey' = {hashedApiKey} AND data ->> 'Deleted' = 'false' ORDER BY version DESC").FirstAsync();
            }
            if (apiKey.StartsWith("test."))
            {
                projectStream = await Context.ProjectStreams.FromSql($"SELECT * FROM project_streams WHERE data ->> 'HashedTestApiKey' = {hashedApiKey} AND data ->> 'Deleted' = 'false' ORDER BY version DESC").FirstAsync();
            }
            return JsonConvert.DeserializeObject<Project>(projectStream?.Data);
        }

        public async Task RemoveProjectAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"{nameof(name)} was null or whitespace.");
            }

            Context.ProjectStreams.RemoveRange(
                await Context.ProjectStreams.FromSql($"SELECT * FROM project_streams WHERE data ->> 'Name' = {name} ORDER BY version DESC").ToListAsync()
            );
        }

        public async Task<(Project, string)> UpdateApiKeyAsync(string userEmail, string environment, int version, string projectId)
        {
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                throw new ArgumentException($"{nameof(userEmail)} was null or whitespace.");
            }
            if (string.IsNullOrWhiteSpace(environment))
            {
                throw new ArgumentException($"{nameof(environment)} was null or whitespace.");
            }

            if (projectId is null)
            {
                throw new ArgumentException($"{nameof(projectId)} was null.");
            }
            if (environment != "live" || environment != "test")
            {
                var currentProjectStream = await GetProjectStreamByProjectIdAsync(projectId);
                ValidateRequestVersionIncremented(version, currentProjectStream);

                var currentProject = JsonConvert.DeserializeObject<Project>(currentProjectStream.Data);
                var uniqueConstraint = await this.GetProjectUniqueConstraintsByProjectIdAsync(currentProject.ProjectId);
                var newApiKeyGuid = Guid.NewGuid().ToString();
                string resultKey = "";

                if (environment == "live")
                {
                    resultKey = "live." + newApiKeyGuid;
                    var newApiKeyPrefix = "live." + newApiKeyGuid.Substring(0, 6);
                    var hashedApiKeyGuid = Crypto.GenerateSHA512Hash(resultKey);
                    currentProject.LiveApiKeyPrefix = newApiKeyPrefix;
                    currentProject.HashedLiveApiKey = hashedApiKeyGuid;
                    uniqueConstraint.HashedLiveApiKey = hashedApiKeyGuid;
                }
                else
                {
                    resultKey = "test." + newApiKeyGuid;
                    var newApiKeyPrefix = "test." + newApiKeyGuid.Substring(0, 6);
                    var hashedApiKeyGuid = Crypto.GenerateSHA512Hash(resultKey);
                    currentProject.TestApiKeyPrefix = newApiKeyPrefix;
                    currentProject.HashedTestApiKey = hashedApiKeyGuid;
                    uniqueConstraint.HashedTestApiKey = hashedApiKeyGuid;
                }

                var updatedProjectStream = new ProjectStream()
                {
                    DatabaseUsername = this.contextTenant.DatabaseUsername,
                    StreamId = currentProjectStream.StreamId,
                    Version = version,
                    Data = JsonConvert.SerializeObject(currentProject),
                    Event = "ApiKeyReset",
                    InsertedAt = DateTime.UtcNow,
                    InsertedBy = userEmail,
                };

                Context.Update(uniqueConstraint);
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
                            case ProjectJsonbConstraintNames.ProjectId_Version:
                                {
                                    throw new ConcurrencyException($"The update version number was outdated. The current stream version is '{currentProjectStream.Version}' and the request update version was '{version}'.");
                                }
                            default:
                                {
                                    throw new EventStreamDataConstraintException("");
                                }
                        }
                    }
                    throw;
                }

                return (currentProject, resultKey);
            }
            throw new ArgumentException($"'{environment}' is not a valid environment name. Expected 'live' or 'test'.");
        }

        public async Task SoftDeleteAsync(string userEmail, string projectId)
        {
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                throw new ArgumentException($"{nameof(userEmail)} was null or whitespace.");
            }
            if (string.IsNullOrWhiteSpace(projectId))
            {
                throw new ArgumentException($"{nameof(projectId)} was null or whitespace.");
            }
            Guid parsedProjectId;
            if (!Guid.TryParse(projectId, out parsedProjectId))
            {
                throw new ArgumentException($"{nameof(projectId)} was not a valid Guid.");
            }

            var currentProjectStream = await GetProjectStreamByProjectIdAsync(projectId);
            var currentProject = JsonConvert.DeserializeObject<Project>(currentProjectStream.Data);
            currentProject.Deleted = true;

            var uniqueConstraint = await this.GetProjectUniqueConstraintsByProjectIdAsync(parsedProjectId);
            var deleted = false;
            var maxConcurrencyAttempts = 3;
            while (!deleted && maxConcurrencyAttempts != 0)
            {
                var updatedProjectStream = new ProjectStream()
                {
                    DatabaseUsername = this.contextTenant.DatabaseUsername,
                    StreamId = currentProjectStream.StreamId,
                    Version = currentProjectStream.Version + 1,
                    Data = JsonConvert.SerializeObject(currentProject),
                    Event = "ProjectDeleted",
                    InsertedAt = DateTime.UtcNow,
                    InsertedBy = userEmail,
                };
                Context.ProjectUniqueConstraints.Remove(await Context.ProjectUniqueConstraints.Where(_ => _.ProjectId == currentProject.ProjectId).SingleAsync());
                Context.ProjectStreams.Add(updatedProjectStream);
                try
                {
                    await Context.SaveChangesAsync();
                    deleted = true;
                    logger.LogInformation($"Project {currentProject.Name} deleted.");
                }
                catch (DbUpdateException ex)
                {
                    var postgresException = ex.InnerException as PostgresException;
                    if (postgresException.SqlState == "23505")
                    {
                        var uniqueIndexViolation = postgresException.ConstraintName;
                        switch (uniqueIndexViolation)
                        {
                            case ProjectJsonbConstraintNames.ProjectId_Version:
                                {
                                    logger.LogError($"The update version number was outdated. The current and updated stream versions are '{currentProjectStream.Version + 1}'.");
                                    maxConcurrencyAttempts--;
                                    continue;
                                }
                        }
                    }
                    throw;
                }
            }
        }

        public async Task<Project> UpdateProjectAsync(string userEmail, string eventName, int version, Project project)
        {
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
                throw new ArgumentException($"{nameof(project)} was null.");
            }

            var currentProjectStream = await GetProjectStreamByProjectIdAsync(project.ProjectId.ToString());
            ValidateRequestVersionIncremented(version, currentProjectStream);

            var outdatedProject = JsonConvert.DeserializeObject<Project>(currentProjectStream.Data);
            project.HashedLiveApiKey = outdatedProject.HashedLiveApiKey;
            project.HashedTestApiKey = outdatedProject.HashedTestApiKey;
            project.LiveApiKeyPrefix = outdatedProject.LiveApiKeyPrefix;
            project.TestApiKeyPrefix = outdatedProject.TestApiKeyPrefix;
            project.Deleted = false;

            var serializedNewProjectData = JsonConvert.SerializeObject(project);
            ValidateDataJsonInequality(currentProjectStream, serializedNewProjectData);

            var uniqueConstraint = await this.GetProjectUniqueConstraintsByProjectIdAsync(project.ProjectId);
            uniqueConstraint.Name = project.Name;
            uniqueConstraint.HashedLiveApiKey = project.HashedLiveApiKey;
            uniqueConstraint.HashedTestApiKey = project.HashedTestApiKey;

            var updatedProjectStream = new ProjectStream()
            {
                DatabaseUsername = this.contextTenant.DatabaseUsername,
                StreamId = currentProjectStream.StreamId,
                Version = version,
                Data = serializedNewProjectData,
                Event = eventName,
                InsertedAt = DateTime.UtcNow,
                InsertedBy = userEmail,
            };

            Context.Update(uniqueConstraint);
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
                }
                throw;
            }
            return project;
        }

        private async Task<ProjectStream> GetProjectStreamByProjectNameAsync(string name)
        {
            return await Context.ProjectStreams.FromSql($"SELECT * FROM project_streams WHERE data ->> 'Name' = {name} AND data ->> 'Deleted' = 'false' ORDER BY version DESC").FirstOrDefaultAsync();
        }

        private static void ValidateDataJsonInequality(ProjectStream currentProjectStream, string serializedNewProjectData)
        {
            var currentJObject = JsonConvert.DeserializeObject<JObject>(currentProjectStream.Data);
            var requestJObject = JsonConvert.DeserializeObject<JObject>(serializedNewProjectData);
            if (JToken.DeepEquals(currentJObject, requestJObject))
            {
                throw new NoOpException("No changes were made from the previous version.");
            }
        }

        private static void ValidateRequestVersionIncremented(int version, ProjectStream currentProjectStream)
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

        private async Task<ProjectStream> GetProjectStreamByProjectIdAsync(string projectId)
        {
            return await Context.ProjectStreams.FromSql($"SELECT * FROM project_streams WHERE data ->> 'ProjectId' = {projectId} AND data -> 'Deleted' = 'false' ORDER BY version DESC").FirstOrDefaultAsync();
        }

        public async Task<ProjectUniqueConstraint> GetProjectUniqueConstraintsByProjectIdAsync(Guid projectId)
        {
            return await Context.ProjectUniqueConstraints.SingleOrDefaultAsync(_ => _.ProjectId == projectId);
        }

        private ProjectUniqueConstraint AddProjectUniqueConstraints(ProjectStream projectStream, Project project)
        {
            var newProjectUniqueConstraint = new ProjectUniqueConstraint
            {
                ProjectId = project.ProjectId,
                DatabaseUsername = contextTenant.DatabaseUsername,
                Name = project.Name,
                HashedLiveApiKey = project.HashedLiveApiKey,
                HashedTestApiKey = project.HashedTestApiKey
            };
            Context.ProjectUniqueConstraints.Add(newProjectUniqueConstraint);
            return newProjectUniqueConstraint;
        }
    }
}