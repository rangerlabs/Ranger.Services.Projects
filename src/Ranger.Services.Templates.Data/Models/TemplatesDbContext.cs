using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ranger.Common;

namespace Ranger.Services.Templates.Data {
  public class TemplatesDbContext : DbContext, IDataProtectionKeyContext {

    private readonly IDataProtectionProvider dataProtectionProvider;
    public TemplatesDbContext (DbContextOptions<TemplatesDbContext> options, IDataProtectionProvider dataProtectionProvider = null) : base (options) {
      this.dataProtectionProvider = dataProtectionProvider;
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    protected override void OnModelCreating (ModelBuilder modelBuilder) {
      base.OnModelCreating (modelBuilder);
      EncryptingDbHelper encryptionHelper = null;
      if (dataProtectionProvider != null) {
        encryptionHelper = new EncryptingDbHelper (this.dataProtectionProvider);
      }

      foreach (var entity in modelBuilder.Model.GetEntityTypes ()) {
        // Remove 'AspNet' prefix and convert table name from PascalCase to snake_case. E.g. AspNetRoleClaims -> role_claims
        entity.Relational ().TableName = entity.Relational ().TableName.Replace ("AspNet", "").ToSnakeCase ();

        // Convert column names from PascalCase to snake_case.
        foreach (var property in entity.GetProperties ()) {
          property.Relational ().ColumnName = property.Name.ToSnakeCase ();
        }

        // Convert primary key names from PascalCase to snake_case. E.g. PK_users -> pk_users
        foreach (var key in entity.GetKeys ()) {
          key.Relational ().Name = key.Relational ().Name.ToSnakeCase ();
        }

        // Convert foreign key names from PascalCase to snake_case.
        foreach (var key in entity.GetForeignKeys ()) {
          key.Relational ().Name = key.Relational ().Name.ToSnakeCase ();
        }

        // Convert index names from PascalCase to snake_case.
        foreach (var index in entity.GetIndexes ()) {
          index.Relational ().Name = index.Relational ().Name.ToSnakeCase ();
        }

        encryptionHelper?.SetEncrytedPropertyAccessMode (entity);
      }
    }
  }
}