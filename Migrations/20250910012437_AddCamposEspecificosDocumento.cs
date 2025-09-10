using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificadorDoc.Migrations
{
    /// <inheritdoc />
    public partial class AddCamposEspecificosDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentProcessingHistories_UserId",
                table: "DocumentProcessingHistories");

            migrationBuilder.AddColumn<string>(
                name: "CodigoInfracao",
                table: "DocumentProcessingHistories",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataInfracao",
                table: "DocumentProcessingHistories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalInfracao",
                table: "DocumentProcessingHistories",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeCondutor",
                table: "DocumentProcessingHistories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroAIT",
                table: "DocumentProcessingHistories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroCNH",
                table: "DocumentProcessingHistories",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrgaoAutuador",
                table: "DocumentProcessingHistories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlacaVeiculo",
                table: "DocumentProcessingHistories",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextoCompleto",
                table: "DocumentProcessingHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextoDefesa",
                table: "DocumentProcessingHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorMulta",
                table: "DocumentProcessingHistories",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentosTransito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NomeArquivo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TipoDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConfiancaClassificacao = table.Column<double>(type: "float", nullable: false),
                    ResumoConteudo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PalavrasChaveEncontradas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TextoCompleto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessadoComSucesso = table.Column<bool>(type: "bit", nullable: false),
                    ErroProcessamento = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProcessadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessadoPor = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    NumeroAIT = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlacaVeiculo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    NomeCondutor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NumeroCNH = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TextoDefesa = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataInfracao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LocalInfracao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CodigoInfracao = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ValorMulta = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    OrgaoAutuador = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProcessadoPorUsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosTransito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentosTransito_AspNetUsers_ProcessadoPor",
                        column: x => x.ProcessadoPor,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentosTransito_AspNetUsers_ProcessadoPorUsuarioId",
                        column: x => x.ProcessadoPorUsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessingHistories_DocumentType",
                table: "DocumentProcessingHistories",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessingHistories_NumeroAIT",
                table: "DocumentProcessingHistories",
                column: "NumeroAIT");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessingHistories_PlacaVeiculo",
                table: "DocumentProcessingHistories",
                column: "PlacaVeiculo");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessingHistories_UserId_ProcessedAt",
                table: "DocumentProcessingHistories",
                columns: new[] { "UserId", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosTransito_NumeroAIT",
                table: "DocumentosTransito",
                column: "NumeroAIT");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosTransito_PlacaVeiculo",
                table: "DocumentosTransito",
                column: "PlacaVeiculo");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosTransito_ProcessadoEm",
                table: "DocumentosTransito",
                column: "ProcessadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosTransito_ProcessadoPor",
                table: "DocumentosTransito",
                column: "ProcessadoPor");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosTransito_ProcessadoPor_ProcessadoEm",
                table: "DocumentosTransito",
                columns: new[] { "ProcessadoPor", "ProcessadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosTransito_ProcessadoPorUsuarioId",
                table: "DocumentosTransito",
                column: "ProcessadoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosTransito_TipoDocumento",
                table: "DocumentosTransito",
                column: "TipoDocumento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentosTransito");

            migrationBuilder.DropIndex(
                name: "IX_DocumentProcessingHistories_DocumentType",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropIndex(
                name: "IX_DocumentProcessingHistories_NumeroAIT",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropIndex(
                name: "IX_DocumentProcessingHistories_PlacaVeiculo",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropIndex(
                name: "IX_DocumentProcessingHistories_UserId_ProcessedAt",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "CodigoInfracao",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "DataInfracao",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "LocalInfracao",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "NomeCondutor",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "NumeroAIT",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "NumeroCNH",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "OrgaoAutuador",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "PlacaVeiculo",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "TextoCompleto",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "TextoDefesa",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "ValorMulta",
                table: "DocumentProcessingHistories");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessingHistories_UserId",
                table: "DocumentProcessingHistories",
                column: "UserId");
        }
    }
}
