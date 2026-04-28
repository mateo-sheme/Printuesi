using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Printuesi.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nipt = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clients", x => x.client_id);
                });

            migrationBuilder.CreateTable(
                name: "supplies",
                columns: table => new
                {
                    supply_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supply_type = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<float>(type: "real", nullable: false),
                    unit = table.Column<string>(type: "text", nullable: false),
                    added = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplies", x => x.supply_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "label_templates",
                columns: table => new
                {
                    label_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paper_size = table.Column<int>(type: "integer", nullable: false),
                    label_name = table.Column<string>(type: "text", nullable: false),
                    pdf_path = table.Column<string>(type: "text", nullable: false),
                    date_format = table.Column<string>(type: "text", nullable: false),
                    expiry_offset_months = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_label_templates", x => x.label_id);
                    table.ForeignKey(
                        name: "fk_label_templates_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "client_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "print_jobs",
                columns: table => new
                {
                    print_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_print_jobs", x => x.print_job_id);
                    table.ForeignKey(
                        name: "fk_print_jobs_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "client_id");
                    table.ForeignKey(
                        name: "fk_print_jobs_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "print_job_objects",
                columns: table => new
                {
                    print_job_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_requested = table.Column<int>(type: "integer", nullable: false),
                    quantity_printed = table.Column<int>(type: "integer", nullable: false),
                    expiry_date = table.Column<string>(type: "text", nullable: false),
                    lot_num = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    print_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_print_job_objects", x => x.print_job_object_id);
                    table.ForeignKey(
                        name: "fk_print_job_objects_label_templates_label_id",
                        column: x => x.label_id,
                        principalTable: "label_templates",
                        principalColumn: "label_id");
                    table.ForeignKey(
                        name: "fk_print_job_objects_print_jobs_print_job_id",
                        column: x => x.print_job_id,
                        principalTable: "print_jobs",
                        principalColumn: "print_job_id");
                });

            migrationBuilder.CreateTable(
                name: "supply_usage_logs",
                columns: table => new
                {
                    supply_usage_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    used_quantity = table.Column<float>(type: "real", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    supply_id = table.Column<Guid>(type: "uuid", nullable: false),
                    print_job_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supply_usage_logs", x => x.supply_usage_log_id);
                    table.ForeignKey(
                        name: "fk_supply_usage_logs_print_jobs_print_job_id",
                        column: x => x.print_job_id,
                        principalTable: "print_jobs",
                        principalColumn: "print_job_id");
                    table.ForeignKey(
                        name: "fk_supply_usage_logs_supplies_supply_id",
                        column: x => x.supply_id,
                        principalTable: "supplies",
                        principalColumn: "supply_id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_label_templates_client_id",
                table: "label_templates",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_job_objects_label_id",
                table: "print_job_objects",
                column: "label_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_job_objects_print_job_id",
                table: "print_job_objects",
                column: "print_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_jobs_client_id",
                table: "print_jobs",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_jobs_created_by",
                table: "print_jobs",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_supply_usage_logs_print_job_id",
                table: "supply_usage_logs",
                column: "print_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_supply_usage_logs_supply_id",
                table: "supply_usage_logs",
                column: "supply_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "print_job_objects");

            migrationBuilder.DropTable(
                name: "supply_usage_logs");

            migrationBuilder.DropTable(
                name: "label_templates");

            migrationBuilder.DropTable(
                name: "print_jobs");

            migrationBuilder.DropTable(
                name: "supplies");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
