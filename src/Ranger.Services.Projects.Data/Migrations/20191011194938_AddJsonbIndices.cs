using Microsoft.EntityFrameworkCore.Migrations;

namespace Ranger.Services.Projects.Data.Migrations
{
    public partial class AddJsonbIndices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //https://www.postgresql.org/docs/9.6/datatype-json.html
            //https://dba.stackexchange.com/questions/161313/creating-a-unique-constraint-from-a-json-object
            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_data_name ON project_streams ((data->>'Name'));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_data_projectid ON project_streams ((data->>'ProjectId'));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_data_apikey ON project_streams ((data->>'ApiKey'));");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE INDEX idx_data_name");
            migrationBuilder.Sql("CREATE INDEX idx_data_projectid");
            migrationBuilder.Sql("CREATE INDEX idx_data_apikey");
        }
    }
}
