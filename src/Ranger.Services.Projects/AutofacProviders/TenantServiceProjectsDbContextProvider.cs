using System;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Ranger.Common;
using Ranger.InternalHttpClient;

namespace Ranger.Services.Projects
{
    public class TenantServiceDbContext
    {
        private readonly ITenantsClient tenantsClient;
        private readonly ILogger<TenantServiceDbContext> logger;
        private readonly CloudSqlOptions cloudSqlOptions;

        public TenantServiceDbContext(ITenantsClient tenantsClient, CloudSqlOptions cloudSqlOptions, ILogger<TenantServiceDbContext> logger)
        {
            this.cloudSqlOptions = cloudSqlOptions;
            this.logger = logger;
            this.tenantsClient = tenantsClient;
        }


        public (DbContextOptions<T> options, ContextTenant contextTenant) GetDbContextOptions<T>(string tenant)
            where T : DbContext
        {
            ContextTenant contextTenant = null;
            try
            {
                contextTenant = this.tenantsClient.GetTenantAsync<ContextTenant>(tenant).Result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "An exception occurred retrieving the ContextTenant object. Cannot construct the tenant specific repository.");
                throw;
            }

            NpgsqlConnectionStringBuilder connectionBuilder = new NpgsqlConnectionStringBuilder(cloudSqlOptions.ConnectionString);
            connectionBuilder.Username = contextTenant.DatabaseUsername;
            connectionBuilder.Password = contextTenant.DatabasePassword;

            var options = new DbContextOptionsBuilder<T>();
            options.UseNpgsql(connectionBuilder.ToString());
            return (options.Options, contextTenant);
        }

    }
}