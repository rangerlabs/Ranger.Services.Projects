using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Ranger.Services.Templates.Data {
    public class DesignTimeTemplatesDbContextFactory : IDesignTimeDbContextFactory<TemplatesDbContext> {
        public TemplatesDbContext CreateDbContext (string[] args) {
            var config = new ConfigurationBuilder ()
                .SetBasePath (System.IO.Directory.GetCurrentDirectory ())
                .AddJsonFile ("appsettings.json")
                .Build ();

            var options = new DbContextOptionsBuilder<TemplatesDbContext> ();
            options.UseNpgsql (config["cloudSql:ConnectionString"]);

            return new TemplatesDbContext (options.Options);
        }
    }
}