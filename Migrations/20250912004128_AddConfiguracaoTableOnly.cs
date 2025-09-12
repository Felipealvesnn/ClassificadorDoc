using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificadorDoc.Migrations
{
    /// <inheritdoc />
    public partial class AddConfiguracaoTableOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configuracoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Chave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioAtualizacao = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuracoes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Configuracoes_Ativo",
                table: "Configuracoes",
                column: "Ativo");

            migrationBuilder.CreateIndex(
                name: "IX_Configuracoes_Categoria",
                table: "Configuracoes",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_Configuracoes_Chave",
                table: "Configuracoes",
                column: "Chave",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Configuracoes");
        }
    }
}
