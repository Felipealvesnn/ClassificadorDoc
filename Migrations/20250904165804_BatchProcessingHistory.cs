using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificadorDoc.Migrations
{
    /// <inheritdoc />
    public partial class BatchProcessingHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BatchProcessingHistoryId",
                table: "DocumentProcessingHistories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BatchProcessingHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalDocuments = table.Column<int>(type: "int", nullable: false),
                    SuccessfulDocuments = table.Column<int>(type: "int", nullable: false),
                    FailedDocuments = table.Column<int>(type: "int", nullable: false),
                    ProcessingDuration = table.Column<TimeSpan>(type: "time", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ProcessingMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PredominantDocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AverageConfidence = table.Column<double>(type: "float", nullable: false),
                    ClassificationSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KeywordsSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchProcessingHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatchProcessingHistories_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessingHistories_BatchProcessingHistoryId",
                table: "DocumentProcessingHistories",
                column: "BatchProcessingHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProcessingHistories_PredominantDocumentType",
                table: "BatchProcessingHistories",
                column: "PredominantDocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProcessingHistories_StartedAt",
                table: "BatchProcessingHistories",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProcessingHistories_Status",
                table: "BatchProcessingHistories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProcessingHistories_UserId",
                table: "BatchProcessingHistories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProcessingHistories_UserId_StartedAt",
                table: "BatchProcessingHistories",
                columns: new[] { "UserId", "StartedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentProcessingHistories_BatchProcessingHistories_BatchProcessingHistoryId",
                table: "DocumentProcessingHistories",
                column: "BatchProcessingHistoryId",
                principalTable: "BatchProcessingHistories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentProcessingHistories_BatchProcessingHistories_BatchProcessingHistoryId",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropTable(
                name: "BatchProcessingHistories");

            migrationBuilder.DropIndex(
                name: "IX_DocumentProcessingHistories_BatchProcessingHistoryId",
                table: "DocumentProcessingHistories");

            migrationBuilder.DropColumn(
                name: "BatchProcessingHistoryId",
                table: "DocumentProcessingHistories");
        }
    }
}
