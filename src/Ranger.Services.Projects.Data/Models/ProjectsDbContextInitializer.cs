using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{
    public class ProjectsDbContextInitializer : IProjectsDbContextInitializer
    {
        private readonly ProjectsDbContext context;

        public ProjectsDbContextInitializer(ProjectsDbContext context)
        {
            this.context = context;
        }

        public bool EnsureCreated()
        {
            return context.Database.EnsureCreated();
        }

        public void Migrate()
        {
            context.Database.Migrate();
        }

        public async Task EnsureRowLevelSecurityApplied()
        {
            var tables = Enum.GetNames(typeof(RowLevelSecureTablesEnum));
            var loginRoleRepository = new LoginRoleRepository<ProjectsDbContext>(context);
            foreach (var table in tables)
            {
                await loginRoleRepository.CreateTenantRlsPolicy(table);
            }
        }
    }

    public interface IProjectsDbContextInitializer
    {
        bool EnsureCreated();
        void Migrate();
        Task EnsureRowLevelSecurityApplied();
    }
}