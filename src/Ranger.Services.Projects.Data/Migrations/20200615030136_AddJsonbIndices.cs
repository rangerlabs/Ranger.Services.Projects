using Microsoft.EntityFrameworkCore.Migrations;

namespace Ranger.Services.Projects.Data.Migrations
{
    public partial class AddJsonbIndices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //https://www.postgresql.org/docs/9.6/datatype-json.html
            //https://dba.stackexchange.com/questions/161313/creating-a-unique-constraint-from-a-json-object
            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_data_projectid_version ON project_streams (tenant_id, (data->>'ProjectId'), version);");
            migrationBuilder.Sql("CREATE INDEX idx_data_name ON project_streams (tenant_id, (data->>'Name'));");
            migrationBuilder.Sql("CREATE INDEX idx_data_hashedliveapikey ON project_streams (tenant_id, (data->>'HashedLiveApiKey'));");
            migrationBuilder.Sql("CREATE INDEX idx_data_hashedtestapikey ON project_streams (tenant_id, (data->>'HashedTestApiKey'));");
            migrationBuilder.Sql("CREATE INDEX idx_data_hashedprojectapikey ON project_streams (tenant_id, (data->>'HashedProjectApiKey'));");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX idx_data_projectid_version");
            migrationBuilder.Sql("DROP INDEX idx_data_name");
            migrationBuilder.Sql("DROP INDEX idx_data_hashedliveapikey");
            migrationBuilder.Sql("DROP INDEX idx_data_hashedtestapikey");
            migrationBuilder.Sql("DROP INDEX idx_data_hashedprojectapikey");
        }
    }
}
