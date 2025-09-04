using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificadorDoc.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantFieldsFromUserProductivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentsProcessed",
                table: "UserProductivities");

            migrationBuilder.DropColumn(
                name: "ErrorCount",
                table: "UserProductivities");

            migrationBuilder.DropColumn(
                name: "SuccessRate",
                table: "UserProductivities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DocumentsProcessed",
                table: "UserProductivities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ErrorCount",
                table: "UserProductivities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "SuccessRate",
                table: "UserProductivities",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
