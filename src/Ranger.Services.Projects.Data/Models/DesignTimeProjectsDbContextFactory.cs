using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Ranger.Services.Projects.Data {
    public class DesignTimeProjectsDbContextFactory : IDesignTimeDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext (string[] args) {
            var config = new ConfigurationBuilder ()
                .SetBasePath (System.IO.Directory.GetCurrentDirectory ())
                .AddJsonFile ("appsettings.json")
                .Build ();

            var options = new DbContextOptionsBuilder<ProjectsDbContext> ();
            options.UseNpgsql (config["cloudSql:ConnectionString"]);

            return new ProjectsDbContext (options.Options);
        }
    }
}