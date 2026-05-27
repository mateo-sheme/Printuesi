using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Printuesi.Server.Migrations
{
    /// <inheritdoc />
    public partial class product_code : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "product_code",
                table: "print_job_objects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "label_templates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "product_code",
                table: "print_job_objects");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "label_templates");
        }
    }
}
