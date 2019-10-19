using Microsoft.EntityFrameworkCore.Migrations;

namespace Ranger.Services.Projects.Data.Migrations
{
    public partial class AddJsonbIndices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //https://www.postgresql.org/docs/9.6/datatype-json.html
            //https://dba.stackexchange.com/questions/161313/creating-a-unique-constraint-from-a-json-object
            migrationBuilder.Sql("CREATE INDEX idx_data_projectid_version ON project_streams (database_username, (data->>'ProjectId'), Version);");
            migrationBuilder.Sql("CREATE INDEX idx_data_name ON project_streams (database_username, (data->>'Name'));");
            migrationBuilder.Sql("CREATE INDEX idx_data_apikey ON project_streams (database_username, (data->>'ApiKey'));");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE INDEX idx_data_name");
            migrationBuilder.Sql("CREATE INDEX idx_data_projectid");
            migrationBuilder.Sql("CREATE INDEX idx_data_apikey");
        }
    }
}
