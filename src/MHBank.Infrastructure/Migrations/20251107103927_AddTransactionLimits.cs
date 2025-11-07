using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MHBank.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentDailyTransferred",
                table: "BankAccounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentMonthlyTransferred",
                table: "BankAccounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyTransferLimit",
                table: "BankAccounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTransactionAt",
                table: "BankAccounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyTransferLimit",
                table: "BankAccounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentDailyTransferred",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "CurrentMonthlyTransferred",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "DailyTransferLimit",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "LastTransactionAt",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "MonthlyTransferLimit",
                table: "BankAccounts");
        }
    }
}
