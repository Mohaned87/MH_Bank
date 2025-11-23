using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MHBank.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKYCSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "KYCApprovedAt",
                table: "BankAccounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KYCStatus",
                table: "BankAccounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "KYCRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByAdminId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CopiedFromRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsAutoVerified = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KYCRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KYCRequests_BankAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KYCRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KYCDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KYCRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Base64Data = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerificationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KYCDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KYCDocuments_KYCRequests_KYCRequestId",
                        column: x => x.KYCRequestId,
                        principalTable: "KYCRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KYCDocuments_KYCRequestId",
                table: "KYCDocuments",
                column: "KYCRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_KYCDocuments_Type",
                table: "KYCDocuments",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_KYCRequests_AccountId",
                table: "KYCRequests",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_KYCRequests_CreatedAt",
                table: "KYCRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_KYCRequests_Status",
                table: "KYCRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_KYCRequests_UserId",
                table: "KYCRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KYCDocuments");

            migrationBuilder.DropTable(
                name: "KYCRequests");

            migrationBuilder.DropColumn(
                name: "KYCApprovedAt",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "KYCStatus",
                table: "BankAccounts");
        }
    }
}
