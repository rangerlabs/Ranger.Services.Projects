using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{
    public class ProjectsDbContext : DbContext, IDataProtectionKeyContext
    {
        public delegate ProjectsDbContext Factory(DbContextOptions<ProjectsDbContext> options);

        private readonly IDataProtectionProvider dataProtectionProvider;
        public ProjectsDbContext(DbContextOptions<ProjectsDbContext> options, IDataProtectionProvider dataProtectionProvider = null) : base(options)
        {
            this.dataProtectionProvider = dataProtectionProvider;
        }

        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
        public DbSet<ProjectStream> ProjectStreams { get; set; }
        public DbSet<ProjectUniqueConstraint> ProjectUniqueConstraints { get; set; }
        public DbSet<ProjectUser> ProjectUsers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            EncryptingDbHelper encryptionHelper = null;
            if (dataProtectionProvider != null)
            {
                encryptionHelper = new EncryptingDbHelper(this.dataProtectionProvider);
            }

            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                // Remove 'AspNet' prefix and convert table name from PascalCase to snake_case. E.g. AspNetRoleClaims -> role_claims
                entity.SetTableName(entity.GetTableName().Replace("AspNet", "").ToSnakeCase());

                // Convert column names from PascalCase to snake_case.
                foreach (var property in entity.GetProperties())
                {
                    property.SetColumnName(property.Name.ToSnakeCase());
                }

                // Convert primary key names from PascalCase to snake_case. E.g. PK_users -> pk_users
                foreach (var key in entity.GetKeys())
                {
                    key.SetName(key.GetName().ToSnakeCase());
                }

                // Convert foreign key names from PascalCase to snake_case.
                foreach (var key in entity.GetForeignKeys())
                {
                    key.SetConstraintName(key.GetConstraintName().ToSnakeCase());
                }

                // Convert index names from PascalCase to snake_case.
                foreach (var index in entity.GetIndexes())
                {
                    index.SetName(index.GetName().ToSnakeCase());
                }

                encryptionHelper?.SetEncrytedPropertyAccessMode(entity);
            }

            modelBuilder.Entity<ProjectUniqueConstraint>().HasIndex(_ => new { _.TenantId, _.Name }).IsUnique();
            modelBuilder.Entity<ProjectUniqueConstraint>().HasIndex(_ => new { _.TenantId, _.HashedLiveApiKey }).IsUnique();
            modelBuilder.Entity<ProjectUniqueConstraint>().HasIndex(_ => new { _.TenantId, _.HashedTestApiKey }).IsUnique();

            modelBuilder.Entity<ProjectUser>().HasIndex(_ => new { _.ProjectId, _.UserId }).IsUnique();
            modelBuilder.Entity<ProjectUser>().HasIndex(_ => new { _.ProjectId, _.Email }).IsUnique();
        }
    }
}