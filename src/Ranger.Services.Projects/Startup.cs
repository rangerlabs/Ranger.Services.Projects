using System.Security.Cryptography.X509Certificates;
using Autofac;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Ranger.ApiUtilities;
using Ranger.Common;
using Ranger.InternalHttpClient;
using Ranger.Monitoring.HealthChecks;
using Ranger.RabbitMQ;
using Ranger.Services.Projects.Data;

namespace Ranger.Services.Projects
{
    public class Startup
    {
        private readonly IWebHostEnvironment Environment;
        private readonly IConfiguration configuration;
        private ILoggerFactory loggerFactory;
        private IBusSubscriber busSubscriber;

        public Startup(IWebHostEnvironment environment, IConfiguration configuration)
        {
            this.Environment = environment;
            this.configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers(options =>
                {
                    options.EnableEndpointRouting = false;
                    options.Filters.Add<OperationCanceledExceptionFilter>();
                })
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.Converters.Add(new StringEnumConverter());
                    options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                });

            services.AddRangerApiVersioning();
            services.ConfigureAutoWrapperModelStateResponseFactory();
            services.AddSwaggerGen("Projects API", "v1");

            services.AddDbContext<ProjectsDbContext>((serviceProvider, options) =>
            {
                options.UseNpgsql(configuration["cloudSql:ConnectionString"]);
            },
                ServiceLifetime.Transient
            );

            var identityAuthority = configuration["httpClient:identityAuthority"];
            services.AddPollyPolicyRegistry();
            services.AddTenantsHttpClient("http://tenants:8082", identityAuthority, "tenantsApi", "cKprgh9wYKWcsm");
            services.AddIdentityHttpClient("http://identity:5000", identityAuthority, "IdentityServerApi", "89pCcXHuDYTXY");
            services.AddSubscriptionsHttpClient("http://subscriptions:8089", identityAuthority, "subscriptionsApi", "4T3SXqXaD6GyGHn4RY");

            services.AddTransient<IProjectsDbContextInitializer, ProjectsDbContextInitializer>();
            services.AddTransient<ILoginRoleRepository<ProjectsDbContext>, LoginRoleRepository<ProjectsDbContext>>();
            services.AddTransient<ProjectsService>();

            services.AddAuthentication("Bearer")
                .AddIdentityServerAuthentication(options =>
                {
                    options.Authority = "http://identity:5000/auth";
                    options.ApiName = "projectsApi";
                    options.RequireHttpsMetadata = false;
                });

            services.AddDataProtection()
                .SetApplicationName("Projects")
                .ProtectKeysWithCertificate(new X509Certificate2(configuration["DataProtectionCertPath:Path"]))
                .UnprotectKeysWithAnyCertificate(new X509Certificate2(configuration["DataProtectionCertPath:Path"]))
                .PersistKeysToDbContext<ProjectsDbContext>();

            services.AddLiveHealthCheck();
            services.AddEntityFrameworkHealthCheck<ProjectsDbContext>();
            services.AddDockerImageTagHealthCheck();
            services.AddRabbitMQHealthCheck();
        }


        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterInstance<CloudSqlOptions>(configuration.GetOptions<CloudSqlOptions>("cloudSql"));
            builder.RegisterType<ProjectsDbContext>().InstancePerDependency();
            builder.RegisterType<TenantServiceDbContext>();
            builder.RegisterType<ProjectUniqueContraintRepository>().As<IProjectUniqueContraintRepository>();
            builder.Register((c, p) =>
            {
                var provider = c.Resolve<TenantServiceDbContext>();
                var (dbContextOptions, contextTenant) = provider.GetDbContextOptions<ProjectsDbContext>(p.TypedAs<string>());
                return new ProjectUsersRepository(contextTenant, new ProjectsDbContext(dbContextOptions), c.Resolve<ILogger<ProjectUsersRepository>>());
            });
            builder.Register((c, p) =>
            {
                var provider = c.Resolve<TenantServiceDbContext>();
                var (dbContextOptions, contextTenant) = provider.GetDbContextOptions<ProjectsDbContext>(p.TypedAs<string>());
                return new ProjectsRepository(contextTenant, new ProjectsDbContext(dbContextOptions), c.Resolve<ILogger<ProjectsRepository>>());
            });
            builder.AddRabbitMq();
        }

        public void Configure(IApplicationBuilder app, IHostApplicationLifetime applicationLifetime)
        {
            app.UseSwagger("v1", "Projects API");
            app.UseAutoWrapper();
            app.UseUnhandedExceptionLogger();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks();
                endpoints.MapLiveTagHealthCheck();
                endpoints.MapEfCoreTagHealthCheck();
                endpoints.MapDockerImageTagHealthCheck();
                endpoints.MapRabbitMQHealthCheck();
            });
            this.busSubscriber = app.UseRabbitMQ(applicationLifetime)
                .SubscribeCommand<InitializeTenant>((c, e) =>
                   new InitializeTenantRejected(e.Message, "")
                )
                .SubscribeCommand<CreateProject>((c, ex) =>
                    new CreateProjectRejected(ex.Message, "")
                )
                .SubscribeCommand<UpdateUserProjects>((c, ex) =>
                    new UpdateUserProjectsRejected(ex.Message, "")
                )
                .SubscribeCommand<EnforceProjectResourceLimits>();
        }
    }
}