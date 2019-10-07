using Microsoft.EntityFrameworkCore.Migrations;
using Ranger.Common;

namespace Ranger.Services.Projects.Data.Migrations
{
    public partial class AddRowLevelSecurity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MultiTenantMigrationSql.CreateTenantRlsPolicy());
            migrationBuilder.Sql(MultiTenantMigrationSql.CreateTenantLoginRole());
            migrationBuilder.Sql(MultiTenantMigrationSql.GrantTenantLoginRoleTablePermissions());
            migrationBuilder.Sql(MultiTenantMigrationSql.GrantTenantLoginRoleSequencePermissions());
            migrationBuilder.Sql(MultiTenantMigrationSql.RevokeTenantLoginRoleSequencePermissions());
            migrationBuilder.Sql(MultiTenantMigrationSql.RevokeTenantLoginRoleTablePermissions());
            migrationBuilder.Sql(MultiTenantMigrationSql.DropTenantLoginRole());
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
