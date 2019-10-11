using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
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
        public DbSet<ProjectStream<Project>> ProjectStreams { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var changedEntities = ChangeTracker
                .Entries()
                .Where(_ => _.State == EntityState.Added ||
                            _.State == EntityState.Modified);

            var (isValid, errors) = Ranger.Services.Projects.Data.JsonUniqueConstraintDbHelper.ValidateAgainstDbContext(changedEntities, this);
            if (isValid)
            {
                return await base.SaveChangesAsync(true, cancellationToken);
            }
            return await Task.FromResult<int>(0);
        }



        public override int SaveChanges()
        {
            var changedEntities = ChangeTracker
                .Entries()
                .Where(_ => _.State == EntityState.Added ||
                            _.State == EntityState.Modified);

            var (isValid, errors) = Ranger.Services.Projects.Data.JsonUniqueConstraintDbHelper.ValidateAgainstDbContext(changedEntities, this);
            if (isValid)
            {
                return base.SaveChanges();
            }
            else return 0;
        }

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
                entity.Relational().TableName = entity.Relational().TableName.Replace("AspNet", "").ToSnakeCase();

                // Convert column names from PascalCase to snake_case.
                foreach (var property in entity.GetProperties())
                {
                    property.Relational().ColumnName = property.Name.ToSnakeCase();
                }

                // Convert primary key names from PascalCase to snake_case. E.g. PK_users -> pk_users
                foreach (var key in entity.GetKeys())
                {
                    key.Relational().Name = key.Relational().Name.ToSnakeCase();
                }

                // Convert foreign key names from PascalCase to snake_case.
                foreach (var key in entity.GetForeignKeys())
                {
                    key.Relational().Name = key.Relational().Name.ToSnakeCase();
                }

                // Convert index names from PascalCase to snake_case.
                foreach (var index in entity.GetIndexes())
                {
                    index.Relational().Name = index.Relational().Name.ToSnakeCase();
                }

                encryptionHelper?.SetEncrytedPropertyAccessMode(entity);
            }

            modelBuilder.Entity<ProjectStream<Project>>().HasIndex(p => new { p.ProjectId, p.Version }).IsUnique(); //Index to ensure uniqueness on writes
            modelBuilder.Entity<ProjectStream<Project>>().HasIndex(p => new { p.Domain, p.ProjectId }).IsUnique(); //Index for efficient read access
        }
    }
}