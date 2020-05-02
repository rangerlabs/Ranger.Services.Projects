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
    public class ProjectsRepository : IProjectsRepository
    {
        private readonly ContextTenant contextTenant;
        private readonly ProjectsDbContext context;
        private readonly ILogger<ProjectsRepository> logger;

        public ProjectsRepository(ContextTenant contextTenant, ProjectsDbContext context, ILogger<ProjectsRepository> logger)
        {
            this.contextTenant = contextTenant;
            this.context = context;
            this.logger = logger;
        }

        public async Task AddProjectAsync(string userEmail, string eventName, Project project)
        {
            var now = DateTime.UtcNow;
            project.CreatedOn = now;
            var newProjectStream = new ProjectStream()
            {
                TenantId = this.contextTenant.TenantId,
                StreamId = Guid.NewGuid(),
                Version = 0,
                Data = JsonConvert.SerializeObject(project),
                Event = eventName,
                InsertedAt = now,
                InsertedBy = userEmail,
            };
            this.AddProjectUniqueConstraints(newProjectStream, project);
            this.context.ProjectStreams.Add(newProjectStream);
            try
            {
                await this.context.SaveChangesAsync();
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
                                throw new EventStreamDataConstraintException("The project name is in use by another project");
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

        public async Task<IEnumerable<(Project project, int version)>> GetProjectsForUser(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException($"{nameof(email)} was null or whitespace");
            }

            var projectStreams = await this.context.ProjectStreams.
            FromSqlInterpolated($@"
                WITH not_deleted AS(
	                SELECT 
	                	ps.id,
	                	ps.tenant_id,
	                	ps.stream_id,
	                	ps.version,
	                	ps.data,
	                	ps.event,
	                	ps.inserted_at,
	                	ps.inserted_by
	                FROM project_streams ps, project_unique_constraints puc
	                WHERE (ps.data ->> 'ProjectId') = puc.project_id::text
                )
                SELECT DISTINCT ON (ps.stream_id)
                	ps.id,
                	ps.tenant_id,
                	ps.stream_id,
                	ps.version,
                	ps.data,
                	ps.event,
                	ps.inserted_at,
                	ps.inserted_by
                FROM not_deleted ps, project_users pu
                WHERE (ps.data ->> 'ProjectId') = pu.project_id::text
                AND email = {email} 
                ORDER BY ps.stream_id, ps.version DESC;").ToListAsync();
            List<(Project project, int version)> projects = new List<(Project project, int version)>();
            foreach (var projectStream in projectStreams)
            {
                projects.Add((JsonConvert.DeserializeObject<Project>(projectStream.Data), projectStream.Version));
            }
            return projects;
        }

        public async Task<(Project project, int version)> GetProjectByName(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new ArgumentException("message", nameof(projectName));
            }

            var projectStream = await this.context.ProjectStreams.
            FromSqlRaw($@"
                WITH not_deleted AS(
	                SELECT 
            	    	ps.id,
            	    	ps.tenant_id,
            	    	ps.stream_id,
            	    	ps.version,
            	    	ps.data,
            	    	ps.event,
            	    	ps.inserted_at,
            	    	ps.inserted_by
            	    FROM project_streams ps, project_unique_constraints puc
            	    WHERE (ps.data ->> 'ProjectId') = puc.project_id::text
               )
               SELECT DISTINCT ON (ps.stream_id) 
              		ps.id,
              		ps.tenant_id,
              		ps.stream_id,
              		ps.version,
            		ps.data,
            		ps.event,
            		ps.inserted_at,
            		ps.inserted_by
                FROM not_deleted ps
                WHERE (ps.data ->> 'Name') = {projectName}
                ORDER BY ps.stream_id, ps.version DESC;").SingleAsync();
            return (JsonConvert.DeserializeObject<Project>(projectStream.Data), projectStream.Version);
        }

        public async Task<IEnumerable<(Project project, int version)>> GetAllProjects()
        {
            var projectStreams = await this.context.ProjectStreams.
            FromSqlRaw(@"
                WITH not_deleted AS(
	                SELECT 
            	    	ps.id,
            	    	ps.tenant_id,
            	    	ps.stream_id,
            	    	ps.version,
            	    	ps.data,
            	    	ps.event,
            	    	ps.inserted_at,
            	    	ps.inserted_by
            	    FROM project_streams ps, project_unique_constraints puc
            	    WHERE (ps.data ->> 'ProjectId') = puc.project_id::text
               )
               SELECT DISTINCT ON (ps.stream_id) 
              		ps.id,
              		ps.tenant_id,
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

        public async Task<Project> GetProjectByProjectIdAsync(Guid projectId)
        {
            var projectStream = await this.context.ProjectStreams.FromSqlInterpolated($"SELECT * FROM project_streams WHERE data ->> 'ProjectId' = {projectId.ToString()} AND data ->> 'Deleted' = 'false' ORDER BY version DESC").FirstOrDefaultAsync();
            return JsonConvert.DeserializeObject<Project>(projectStream.Data);
        }

        public async Task<string> GetProjectIdByCurrentNameAsync(string name)
        {
            return await this.context.ProjectUniqueConstraints.Where(_ => _.Name == name).Select(_ => _.ProjectId.ToString()).SingleOrDefaultAsync();
        }

        public async Task<Project> GetProjectByApiKeyAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException($"{nameof(apiKey)} was null or whitespace");
            }

            var hashedApiKey = Crypto.GenerateSHA512Hash(apiKey);

            ProjectStream projectStream = null;
            if (apiKey.StartsWith("live."))
            {
                projectStream = await this.context.ProjectStreams.FromSqlInterpolated($"SELECT * FROM project_streams WHERE data ->> 'HashedLiveApiKey' = {hashedApiKey} AND data ->> 'Deleted' = 'false' ORDER BY version DESC").FirstAsync();
            }
            if (apiKey.StartsWith("test."))
            {
                projectStream = await this.context.ProjectStreams.FromSqlInterpolated($"SELECT * FROM project_streams WHERE data ->> 'HashedTestApiKey' = {hashedApiKey} AND data ->> 'Deleted' = 'false' ORDER BY version DESC").FirstAsync();
            }
            return JsonConvert.DeserializeObject<Project>(projectStream?.Data);
        }

        public async Task<(Project, string)> UpdateApiKeyAsync(string userEmail, EnvironmentEnum environment, int version, Guid projectId)
        {
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                throw new ArgumentException($"{nameof(userEmail)} was null or whitespace");
            }

            var environmentString = Enum.GetName(typeof(EnvironmentEnum), environment).ToLowerInvariant();
            var currentProjectStream = await GetProjectStreamByProjectIdAsync(projectId);
            if (!(currentProjectStream is null))
            {
                ValidateRequestVersionIncremented(version, currentProjectStream);

                var currentProject = JsonConvert.DeserializeObject<Project>(currentProjectStream.Data);
                var uniqueConstraint = await this.GetProjectUniqueConstraintsByProjectIdAsync(currentProject.ProjectId);
                var newApiKeyGuid = Guid.NewGuid().ToString();
                string resultKey = "";

                if (environmentString == "live")
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
                    TenantId = this.contextTenant.TenantId,
                    StreamId = currentProjectStream.StreamId,
                    Version = version,
                    Data = JsonConvert.SerializeObject(currentProject),
                    Event = "ApiKeyReset",
                    InsertedAt = DateTime.UtcNow,
                    InsertedBy = userEmail,
                };

                this.context.Update(uniqueConstraint);
                this.context.ProjectStreams.Add(updatedProjectStream);
                try
                {
                    await this.context.SaveChangesAsync();
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
                                    throw new ConcurrencyException($"The update version number was outdated. The current stream version is '{currentProjectStream.Version}' and the request update version was '{version}'");
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
            else
            {
                throw new RangerException($"No project was found for project id '{projectId}'");
            }
        }

        public async Task SoftDeleteAsync(string userEmail, Guid projectId)
        {
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                throw new ArgumentException($"{nameof(userEmail)} was null or whitespace");
            }

            var currentProjectStream = await GetProjectStreamByProjectIdAsync(projectId);
            if (!(currentProjectStream is null))
            {
                var currentProject = JsonConvert.DeserializeObject<Project>(currentProjectStream.Data);
                currentProject.Deleted = true;

                //Make 3 attempts to delete the project in the case that updates were made before the delete request was submitted.
                var deleted = false;
                var maxConcurrencyAttempts = 3;
                while (!deleted && maxConcurrencyAttempts != 0)
                {
                    var updatedProjectStream = new ProjectStream()
                    {
                        TenantId = this.contextTenant.TenantId,
                        StreamId = currentProjectStream.StreamId,
                        Version = currentProjectStream.Version + 1,
                        Data = JsonConvert.SerializeObject(currentProject),
                        Event = "ProjectDeleted",
                        InsertedAt = DateTime.UtcNow,
                        InsertedBy = userEmail,
                    };
                    this.context.ProjectUniqueConstraints.Remove(await this.context.ProjectUniqueConstraints.Where(_ => _.ProjectId == currentProject.ProjectId).SingleAsync());
                    this.context.ProjectStreams.Add(updatedProjectStream);
                    try
                    {
                        await this.context.SaveChangesAsync();
                        deleted = true;
                        logger.LogInformation($"Project {currentProject.Name} deleted");
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
                                        logger.LogError($"The update version number was outdated. The current and updated stream versions are '{currentProjectStream.Version + 1}'");
                                        maxConcurrencyAttempts--;
                                        continue;
                                    }
                            }
                        }
                        throw;
                    }
                }
                if (!deleted)
                {
                    throw new ConcurrencyException($"After '{maxConcurrencyAttempts}' attempts, the version was still outdated. Too many updates have been applied in a short period of time. The current stream version is '{currentProjectStream.Version + 1}'. The project was not deleted");
                }
            }
            else
            {
                throw new RangerException($"No project was found for project id '{projectId}'");
            }
        }

        public async Task<Project> UpdateProjectAsync(string userEmail, string eventName, int version, Project project)
        {
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                throw new ArgumentException($"{nameof(userEmail)} was null or whitespace");
            }

            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException($"{nameof(eventName)} was null or whitespace");
            }

            if (project is null)
            {
                throw new ArgumentException($"{nameof(project)} was null");
            }

            var currentProjectStream = await GetProjectStreamByProjectIdAsync(project.ProjectId);
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
            uniqueConstraint.Name = project.Name.ToLowerInvariant();
            uniqueConstraint.HashedLiveApiKey = project.HashedLiveApiKey;
            uniqueConstraint.HashedTestApiKey = project.HashedTestApiKey;

            var updatedProjectStream = new ProjectStream()
            {
                TenantId = this.contextTenant.TenantId,
                StreamId = currentProjectStream.StreamId,
                Version = version,
                Data = serializedNewProjectData,
                Event = eventName,
                InsertedAt = DateTime.UtcNow,
                InsertedBy = userEmail,
            };

            this.context.Update(uniqueConstraint);
            this.context.ProjectStreams.Add(updatedProjectStream);
            try
            {
                await this.context.SaveChangesAsync();
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
                                throw new EventStreamDataConstraintException("The project name is in use by another project");
                            }
                        case ProjectJsonbConstraintNames.ProjectId_Version:
                            {
                                throw new ConcurrencyException($"The update version number was outdated. The current stream version is '{currentProjectStream.Version}' and the request update version was '{version}'");
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
            var projectId = (await this.context.ProjectUniqueConstraints.Where(_ => _.Name == name.ToLowerInvariant()).SingleOrDefaultAsync())?.ProjectId;
            if (projectId is null)
            {
                return null;
            }
            return await this.context.ProjectStreams.FromSqlInterpolated($"SELECT * FROM project_streams WHERE data ->> 'Name' = {projectId.ToString()} ORDER BY version DESC").FirstOrDefaultAsync();
        }

        private static void ValidateDataJsonInequality(ProjectStream currentProjectStream, string serializedNewProjectData)
        {
            var currentJObject = JsonConvert.DeserializeObject<JObject>(currentProjectStream.Data);
            var requestJObject = JsonConvert.DeserializeObject<JObject>(serializedNewProjectData);
            if (JToken.DeepEquals(currentJObject, requestJObject))
            {
                throw new NoOpException("No changes were made from the previous version");
            }
        }

        private static void ValidateRequestVersionIncremented(int version, ProjectStream currentProjectStream)
        {
            if (version - currentProjectStream.Version > 1)
            {
                throw new ConcurrencyException($"The update version number was too high. The current stream version is '{currentProjectStream.Version}' and the request update version was '{version}'");
            }
            if (version - currentProjectStream.Version <= 0)
            {
                throw new ConcurrencyException($"The update version number was outdated. The current stream version is '{currentProjectStream.Version}' and the request update version was '{version}'");
            }
        }

        private async Task<ProjectStream> GetProjectStreamByProjectIdAsync(Guid projectId)
        {
            return await this.context.ProjectStreams.FromSqlInterpolated($"SELECT * FROM project_streams WHERE data ->> 'ProjectId' = {projectId.ToString()} AND data -> 'Deleted' = 'false' ORDER BY version DESC").FirstOrDefaultAsync();
        }

        public async Task<ProjectUniqueConstraint> GetProjectUniqueConstraintsByProjectIdAsync(Guid projectId)
        {
            return await this.context.ProjectUniqueConstraints.SingleOrDefaultAsync(_ => _.ProjectId == projectId);
        }

        private void AddProjectUniqueConstraints(ProjectStream projectStream, Project project)
        {
            var newProjectUniqueConstraint = new ProjectUniqueConstraint
            {
                ProjectId = project.ProjectId,
                TenantId = contextTenant.TenantId,
                Name = project.Name.ToLowerInvariant(),
                HashedLiveApiKey = project.HashedLiveApiKey,
                HashedTestApiKey = project.HashedTestApiKey
            };
            this.context.ProjectUniqueConstraints.Add(newProjectUniqueConstraint);
        }
    }
}