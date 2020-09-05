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
        private readonly ILoggerFactory loggerFactory;

        public TenantServiceDbContextProvider(IConnectionMultiplexer connectionMultiplexer, ITenantsHttpClient tenantsClient, CloudSqlOptions cloudSqlOptions, ILoggerFactory loggerFactory)
        {
            this.cloudSqlOptions = cloudSqlOptions;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger<TenantServiceDbContextProvider>();
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
                logger.LogDebug("Retrieving tenant password from Tenants service");
                var apiResponse = tenantsClient.GetTenantByIdAsync<ContextTenant>(tenantId).Result;
                connectionBuilder.Password = apiResponse.Result.DatabasePassword;
                redisDb.StringSet(tenantDbKey, apiResponse.Result.DatabasePassword, TimeSpan.FromHours(1));
                contextTenant = apiResponse.Result;
            }
            else
            {
                logger.LogDebug("Utilizing cached tenant password");
                connectionBuilder.Password = redisValue;
                contextTenant = new ContextTenant(tenantId, redisValue, true);
            }

            var options = new DbContextOptionsBuilder<T>();
            options.UseNpgsql(connectionBuilder.ToString());
            options.UseLoggerFactory(loggerFactory);
            return (options.Options, contextTenant);
        }
    }
}