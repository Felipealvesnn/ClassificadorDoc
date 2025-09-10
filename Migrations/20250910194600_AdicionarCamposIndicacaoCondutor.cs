using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificadorDoc.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCamposIndicacaoCondutor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IndicacaoCNH",
                table: "DocumentosTransito",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndicacaoCPF",
                table: "DocumentosTransito",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndicacaoNome",
                table: "DocumentosTransito",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndicacaoRG",
                table: "DocumentosTransito",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequerenteCPF",
                table: "DocumentosTransito",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequerenteEndereco",
                table: "DocumentosTransito",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequerenteNome",
                table: "DocumentosTransito",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequerenteRG",
                table: "DocumentosTransito",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndicacaoCNH",
                table: "DocumentosTransito");

            migrationBuilder.DropColumn(
                name: "IndicacaoCPF",
                table: "DocumentosTransito");

            migrationBuilder.DropColumn(
                name: "IndicacaoNome",
                table: "DocumentosTransito");

            migrationBuilder.DropColumn(
                name: "IndicacaoRG",
                table: "DocumentosTransito");

            migrationBuilder.DropColumn(
                name: "RequerenteCPF",
                table: "DocumentosTransito");

            migrationBuilder.DropColumn(
                name: "RequerenteEndereco",
                table: "DocumentosTransito");

            migrationBuilder.DropColumn(
                name: "RequerenteNome",
                table: "DocumentosTransito");

            migrationBuilder.DropColumn(
                name: "RequerenteRG",
                table: "DocumentosTransito");
        }
    }
}
