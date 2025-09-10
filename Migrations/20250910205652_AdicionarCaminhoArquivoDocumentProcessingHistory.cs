using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificadorDoc.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCaminhoArquivoDocumentProcessingHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaminhoArquivo",
                table: "DocumentProcessingHistories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaminhoArquivo",
                table: "DocumentProcessingHistories");
        }
    }
}
