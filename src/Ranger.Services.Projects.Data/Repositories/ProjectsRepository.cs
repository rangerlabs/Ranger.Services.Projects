using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
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

        public async Task<IEnumerable<(Project project, int version)>> GetProjectsForUser(string email, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException($"{nameof(email)} was null or whitespace");
            }

            var projectStreams = await this.context.ProjectStreams.
            FromSqlInterpolated($@"
                SELECT * FROM (
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
                        WHERE (ps.data ->> 'Id') = puc.project_id::text
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
                    WHERE (ps.data ->> 'Id') = pu.project_id::text
                    AND email = {email}
                    ORDER BY ps.stream_id, ps.version DESC) AS projectstreams").ToListAsync(cancellationToken);
            List<(Project project, int version)> projects = new List<(Project project, int version)>();
            foreach (var projectStream in projectStreams)
            {
                projects.Add((JsonConvert.DeserializeObject<Project>(projectStream.Data), projectStream.Version));
            }
            return projects;
        }

        public async Task<(Project project, int version)> GetProjectByName(string projectName, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new ArgumentException("message", nameof(projectName));
            }

            var projectStream = await GetNotDeletedProjectStreamByProjectNameAsync(projectName, cancellationToken);
            return (JsonConvert.DeserializeObject<Project>(projectStream.Data), projectStream.Version);
        }

        public async Task<IEnumerable<(Project project, int version)>> GetAllNotDeletedProjects(CancellationToken cancellationToken = default(CancellationToken))
        {
            var projectStreams = await this.context.ProjectStreams.
            FromSqlRaw(@"
                SELECT * FROM (
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
                        WHERE (ps.data ->> 'Id') = puc.project_id::text
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
                    ORDER BY ps.stream_id, ps.version DESC) AS projectstreams").ToListAsync(cancellationToken);
            List<(Project project, int version)> projects = new List<(Project project, int version)>();
            foreach (var projectStream in projectStreams)
            {
                projects.Add((JsonConvert.DeserializeObject<Project>(projectStream.Data), projectStream.Version));
            }
            return projects;
        }



        public async Task<Project> GetProjectByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var projectStream = await GetNotDeletedProjectStreamByProjectIdAsync(projectId, cancellationToken);
            return JsonConvert.DeserializeObject<Project>(projectStream.Data);
        }

        public async Task<string> GetProjectIdByCurrentNameAsync(string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.context.ProjectUniqueConstraints.Where(_ => _.Name == name).Select(_ => _.ProjectId.ToString()).SingleOrDefaultAsync();
        }

        public async Task<Project> GetProjectByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException($"{nameof(apiKey)} was null or whitespace");
            }

            ProjectStream projectStream = null;
            if (apiKey.StartsWith("live."))
            {
                projectStream = await GetNotDeletedProjectStreamByApiKeyAsync(ApiKeyPurposeEnum.LIVE, apiKey, cancellationToken);
            }
            else if (apiKey.StartsWith("test."))
            {
                projectStream = await GetNotDeletedProjectStreamByApiKeyAsync(ApiKeyPurposeEnum.TEST, apiKey, cancellationToken);
            }
            else if (apiKey.StartsWith("proj."))
            {
                projectStream = await GetNotDeletedProjectStreamByApiKeyAsync(ApiKeyPurposeEnum.PROJ, apiKey, cancellationToken);
            }
            return JsonConvert.DeserializeObject<Project>(projectStream?.Data);
        }

        public async Task<(Project, string, string)> UpdateApiKeyAsync(string userEmail, ApiKeyPurposeEnum environment, int version, Guid projectId)
        {
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                throw new ArgumentException($"{nameof(userEmail)} was null or whitespace");
            }

            var environmentString = Enum.GetName(typeof(ApiKeyPurposeEnum), environment).ToLowerInvariant();
            var currentProjectStream = await GetNotDeletedProjectStreamByProjectIdAsync(projectId);
            if (!(currentProjectStream is null))
            {
                ValidateRequestVersionIncremented(version, currentProjectStream);

                var currentProject = JsonConvert.DeserializeObject<Project>(currentProjectStream.Data);
                var uniqueConstraint = await this.GetProjectUniqueConstraintsByProjectIdAsync(currentProject.Id);
                var newApiKeyGuid = Guid.NewGuid().ToString("N");
                string resultKey = "";
                string oldHashedApiKey = "";

                if (environmentString == "live")
                {
                    oldHashedApiKey = new String(currentProject.HashedLiveApiKey);
                    resultKey = "live." + newApiKeyGuid;
                    var newApiKeyPrefix = "live." + newApiKeyGuid.Substring(0, 6);
                    var hashedApiKeyGuid = Crypto.GenerateSHA512Hash(resultKey);
                    currentProject.LiveApiKeyPrefix = newApiKeyPrefix;
                    currentProject.HashedLiveApiKey = hashedApiKeyGuid;
                    uniqueConstraint.HashedLiveApiKey = hashedApiKeyGuid;
                }
                else if (environmentString == "test")
                {
                    oldHashedApiKey = new String(currentProject.HashedTestApiKey);
                    resultKey = "test." + newApiKeyGuid;
                    var newApiKeyPrefix = "test." + newApiKeyGuid.Substring(0, 6);
                    var hashedApiKeyGuid = Crypto.GenerateSHA512Hash(resultKey);
                    currentProject.TestApiKeyPrefix = newApiKeyPrefix;
                    currentProject.HashedTestApiKey = hashedApiKeyGuid;
                    uniqueConstraint.HashedTestApiKey = hashedApiKeyGuid;
                }
                else if (environmentString == "proj")
                {
                    oldHashedApiKey = new String(currentProject.ProjectApiKeyPrefix);
                    resultKey = "proj." + newApiKeyGuid;
                    var newApiKeyPrefix = "proj." + newApiKeyGuid.Substring(0, 6);
                    var hashedApiKeyGuid = Crypto.GenerateSHA512Hash(resultKey);
                    currentProject.ProjectApiKeyPrefix = newApiKeyPrefix;
                    currentProject.HashedProjectApiKey = hashedApiKeyGuid;
                    uniqueConstraint.HashedProjectApiKey = hashedApiKeyGuid;
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
                                    throw new ConcurrencyException($"The version number '{version}' was outdated. The current resource is at version '{currentProjectStream.Version}'. Re-request the resource to view the latest changes");
                                }
                            default:
                                {
                                    throw new EventStreamDataConstraintException("");
                                }
                        }
                    }
                    throw;
                }

                return (currentProject, resultKey, oldHashedApiKey);
            }
            else
            {
                throw new RangerException($"No project was found for project id '{projectId}'");
            }
        }

        public async Task<Project> SoftDeleteAsync(string userEmail, Guid projectId)
        {
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                throw new ArgumentException($"{nameof(userEmail)} was null or whitespace");
            }

            var currentProjectStream = await GetNotDeletedProjectStreamByProjectIdAsync(projectId);
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
                    this.context.ProjectUniqueConstraints.Remove(await this.context.ProjectUniqueConstraints.Where(_ => _.ProjectId == currentProject.Id).SingleAsync());
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
                return currentProject;
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

            var currentProjectStream = await GetNotDeletedProjectStreamByProjectIdAsync(project.Id);
            if (currentProjectStream is null)
            {
                throw new RangerException("The project was not found. PUT can only be used to update existing projects");
            }
            ValidateRequestVersionIncremented(version, currentProjectStream);

            var outdatedProject = JsonConvert.DeserializeObject<Project>(currentProjectStream.Data);
            project.HashedLiveApiKey = outdatedProject.HashedLiveApiKey;
            project.HashedTestApiKey = outdatedProject.HashedTestApiKey;
            project.HashedProjectApiKey = outdatedProject.HashedProjectApiKey;
            project.LiveApiKeyPrefix = outdatedProject.LiveApiKeyPrefix;
            project.TestApiKeyPrefix = outdatedProject.TestApiKeyPrefix;
            project.ProjectApiKeyPrefix = outdatedProject.ProjectApiKeyPrefix;
            project.CreatedOn = outdatedProject.CreatedOn;
            project.Deleted = false;

            var serializedNewProjectData = JsonConvert.SerializeObject(project);
            ValidateDataJsonInequality(currentProjectStream, serializedNewProjectData);

            var uniqueConstraint = await this.GetProjectUniqueConstraintsByProjectIdAsync(project.Id);
            uniqueConstraint.Name = project.Name.ToLowerInvariant();
            uniqueConstraint.HashedLiveApiKey = project.HashedLiveApiKey;
            uniqueConstraint.HashedTestApiKey = project.HashedTestApiKey;
            uniqueConstraint.HashedProjectApiKey = project.HashedProjectApiKey;

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
                                throw new ConcurrencyException($"The version number '{version}' was outdated. The current resource is at version '{currentProjectStream.Version}'. Re-request the resource to view the latest changes");
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

        private async Task<ProjectStream> GetNotDeletedProjectStreamByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.context.ProjectStreams
            .FromSqlInterpolated($@"
                SELECT * FROM (
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
                        WHERE puc.project_id = {projectId} 
                        AND (ps.data ->> 'Id') = puc.project_id::text
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
                    ORDER BY ps.stream_id, ps.version DESC) AS projectstreams").FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<ProjectStream> GetNotDeletedProjectStreamByApiKeyAsync(ApiKeyPurposeEnum apiKeyPurpose, string apiKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            var columnName = apiKeyPurpose switch
            {
                ApiKeyPurposeEnum.LIVE => "hashed_live_api_key",
                ApiKeyPurposeEnum.TEST => "hashed_test_api_key",
                ApiKeyPurposeEnum.PROJ => "hashed_project_api_key",
                _ => throw new ArgumentException("Invalid Api Key purpose")
            };
            var hashedApiKey = Crypto.GenerateSHA512Hash(apiKey);
            return await this.context.ProjectStreams
            .FromSqlRaw($@"
                SELECT * FROM (
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
                        WHERE puc.{columnName} = @apiKey
                        AND (ps.data ->> 'Id') = puc.project_id::text
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
                    ORDER BY ps.stream_id, ps.version DESC) AS projectstream", new NpgsqlParameter("@apiKey", hashedApiKey)).FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<ProjectStream> GetNotDeletedProjectStreamByProjectNameAsync(string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"'{nameof(name)}' cannot be null or whitespace", nameof(name));
            }

            return await this.context.ProjectStreams
            .FromSqlInterpolated($@"
                SELECT * FROM (
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
                        WHERE puc.name = {name.ToLowerInvariant()} 
                        AND (ps.data ->> 'Id') = puc.project_id::text
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
                    ORDER BY ps.stream_id, ps.version DESC) AS projectstreams").FirstOrDefaultAsync(cancellationToken);
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
                throw new ConcurrencyException($"The version number '{version}' was too high. The current resource is at version '{currentProjectStream.Version}'");
            }
            if (version - currentProjectStream.Version <= 0)
            {
                throw new ConcurrencyException($"The version number '{version}' was outdated. The current resource is at version '{currentProjectStream.Version}'. Re-request the resource to view the latest changes");
            }
        }

        public async Task<ProjectUniqueConstraint> GetProjectUniqueConstraintsByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.context.ProjectUniqueConstraints.SingleOrDefaultAsync(_ => _.ProjectId == projectId, cancellationToken);
        }

        private void AddProjectUniqueConstraints(ProjectStream projectStream, Project project)
        {
            var newProjectUniqueConstraint = new ProjectUniqueConstraint
            {
                ProjectId = project.Id,
                TenantId = contextTenant.TenantId,
                Name = project.Name.ToLowerInvariant(),
                HashedLiveApiKey = project.HashedLiveApiKey,
                HashedTestApiKey = project.HashedTestApiKey,
                HashedProjectApiKey = project.HashedProjectApiKey
            };
            this.context.ProjectUniqueConstraints.Add(newProjectUniqueConstraint);
        }
    }
}