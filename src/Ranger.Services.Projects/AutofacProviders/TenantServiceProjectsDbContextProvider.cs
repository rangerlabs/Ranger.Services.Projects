using System;
using AutoWrapper.Wrappers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Ranger.Common;
using Ranger.InternalHttpClient;
using StackExchange.Redis;

namespace Ranger.Services.Projects
{
    public class TenantServiceDbContextProvider
    {
        private readonly ITenantsHttpClient tenantsClient;
        private readonly IDatabase redisDb;
        private readonly ILogger<TenantServiceDbContextProvider> logger;
        private readonly CloudSqlOptions cloudSqlOptions;

        public TenantServiceDbContextProvider(IConnectionMultiplexer connectionMultiplexer, ITenantsHttpClient tenantsClient, CloudSqlOptions cloudSqlOptions, ILogger<TenantServiceDbContextProvider> logger)
        {
            this.cloudSqlOptions = cloudSqlOptions;
            this.logger = logger;
            this.tenantsClient = tenantsClient;
            redisDb = connectionMultiplexer.GetDatabase();
        }

        public (DbContextOptions<T> options, ContextTenant contextTenant) GetDbContextOptions<T>(string tenantId)
            where T : DbContext
        {
            NpgsqlConnectionStringBuilder connectionBuilder = new NpgsqlConnectionStringBuilder(cloudSqlOptions.ConnectionString);
            connectionBuilder.Username = tenantId;
            var tenantDbKey = RedisKeys.TenantDbPassword(tenantId);

            ContextTenant contextTenant = null;
            string redisValue = redisDb.StringGet(tenantDbKey);
            if (string.IsNullOrWhiteSpace(redisValue))
            {
                var apiResponse = tenantsClient.GetTenantByIdAsync<ContextTenant>(tenantId).Result;
                connectionBuilder.Password = apiResponse.Result.DatabasePassword;
                redisDb.StringSet(tenantDbKey, apiResponse.Result.DatabasePassword, TimeSpan.FromHours(1));
                contextTenant = apiResponse.Result;
            }
            else
            {
                connectionBuilder.Password = redisValue;
                contextTenant = new ContextTenant(tenantDbKey, redisValue, true);
            }

            var options = new DbContextOptionsBuilder<T>();
            options.UseNpgsql(connectionBuilder.ToString());
            return (options.Options, contextTenant);
        }
    }
}