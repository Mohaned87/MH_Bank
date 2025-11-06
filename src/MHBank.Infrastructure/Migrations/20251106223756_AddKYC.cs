using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MHBank.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKYC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdDocumentPath",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KycRejectionReason",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KycStatus",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "KycSubmittedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KycVerifiedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelfiePath",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdDocumentPath",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycRejectionReason",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycSubmittedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycVerifiedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SelfiePath",
                table: "Users");
        }
    }
}
