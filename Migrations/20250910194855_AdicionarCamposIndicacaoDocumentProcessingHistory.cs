using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificadorDoc.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCamposIndicacaoDocumentProcessingHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IndicacaoCNH",
                table: "DocumentProcessingHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndicacaoCPF",
                table: "DocumentProcessingHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndicacaoNome",
                table: "DocumentProcessingHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndicacaoRG",
                table: "DocumentProcessingHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequerenteCPF",
                table: "DocumentProcessingHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequerenteEndereco",
                table: "DocumentProcessingHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequerenteNome",
                table: "DocumentProcessingHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequerenteRG",
                table: "DocumentProcessingHistories",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndicacaoCNH",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "IndicacaoCPF",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "IndicacaoNome",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "IndicacaoRG",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "RequerenteCPF",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "RequerenteEndereco",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "RequerenteNome",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "RequerenteRG",
                table: "DocumentProcessingHistories");
        }
    }
}
