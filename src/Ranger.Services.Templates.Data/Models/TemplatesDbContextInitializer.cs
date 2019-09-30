using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ranger.Common;

namespace Ranger.Services.Templates.Data
{
    public class TemplatesDbContextInitializer : ITemplatesDbContextInitializer
    {
        private readonly TemplatesDbContext context;

        public TemplatesDbContextInitializer(TemplatesDbContext context)
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
            var loginRoleRepository = new LoginRoleRepository<TemplatesDbContext>(context);
            foreach (var table in tables)
            {
                await loginRoleRepository.CreateTenantRlsPolicy(table);
            }
        }
    }

    public interface ITemplatesDbContextInitializer
    {
        bool EnsureCreated();
        void Migrate();
        Task EnsureRowLevelSecurityApplied();
    }
}