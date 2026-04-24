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
                name: "Clients",
                columns: table => new
                {
                    Client_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NIPT = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Client_ID);
                });

            migrationBuilder.CreateTable(
                name: "Supplies",
                columns: table => new
                {
                    Supply_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SupplyType = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<float>(type: "real", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Added = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Supplies", x => x.Supply_ID);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    User_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.User_ID);
                });

            migrationBuilder.CreateTable(
                name: "LabelTemplates",
                columns: table => new
                {
                    Label_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Client_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PaperSize = table.Column<int>(type: "int", nullable: false),
                    Label_Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pdf_Path = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date_Format = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Expiry_Offset_Months = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelTemplates", x => x.Label_ID);
                    table.ForeignKey(
                        name: "FK_LabelTemplates_Clients_Client_ID",
                        column: x => x.Client_ID,
                        principalTable: "Clients",
                        principalColumn: "Client_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrintJobs",
                columns: table => new
                {
                    PrintJob_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Completed_At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Created_By = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Client_ID = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobs", x => x.PrintJob_ID);
                    table.ForeignKey(
                        name: "FK_PrintJobs_Clients_Client_ID",
                        column: x => x.Client_ID,
                        principalTable: "Clients",
                        principalColumn: "Client_ID");
                    table.ForeignKey(
                        name: "FK_PrintJobs_Users_Created_By",
                        column: x => x.Created_By,
                        principalTable: "Users",
                        principalColumn: "User_ID");
                });

            migrationBuilder.CreateTable(
                name: "PrintJobObjects",
                columns: table => new
                {
                    PrintJobObject_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Quantity_Requested = table.Column<int>(type: "int", nullable: false),
                    Quantity_Printed = table.Column<int>(type: "int", nullable: false),
                    Expiry_Date = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Lot_Num = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PrintJob_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Label_ID = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobObjects", x => x.PrintJobObject_ID);
                    table.ForeignKey(
                        name: "FK_PrintJobObjects_LabelTemplates_Label_ID",
                        column: x => x.Label_ID,
                        principalTable: "LabelTemplates",
                        principalColumn: "Label_ID");
                    table.ForeignKey(
                        name: "FK_PrintJobObjects_PrintJobs_PrintJob_ID",
                        column: x => x.PrintJob_ID,
                        principalTable: "PrintJobs",
                        principalColumn: "PrintJob_ID");
                });

            migrationBuilder.CreateTable(
                name: "SupplyUsageLogs",
                columns: table => new
                {
                    SupplyUsageLog_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Used_Quantity = table.Column<float>(type: "real", nullable: false),
                    Used_At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Supply_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PrintJob_ID = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyUsageLogs", x => x.SupplyUsageLog_ID);
                    table.ForeignKey(
                        name: "FK_SupplyUsageLogs_PrintJobs_PrintJob_ID",
                        column: x => x.PrintJob_ID,
                        principalTable: "PrintJobs",
                        principalColumn: "PrintJob_ID");
                    table.ForeignKey(
                        name: "FK_SupplyUsageLogs_Supplies_Supply_ID",
                        column: x => x.Supply_ID,
                        principalTable: "Supplies",
                        principalColumn: "Supply_ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabelTemplates_Client_ID",
                table: "LabelTemplates",
                column: "Client_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobObjects_Label_ID",
                table: "PrintJobObjects",
                column: "Label_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobObjects_PrintJob_ID",
                table: "PrintJobObjects",
                column: "PrintJob_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_Client_ID",
                table: "PrintJobs",
                column: "Client_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_Created_By",
                table: "PrintJobs",
                column: "Created_By");

            migrationBuilder.CreateIndex(
                name: "IX_SupplyUsageLogs_PrintJob_ID",
                table: "SupplyUsageLogs",
                column: "PrintJob_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SupplyUsageLogs_Supply_ID",
                table: "SupplyUsageLogs",
                column: "Supply_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrintJobObjects");

            migrationBuilder.DropTable(
                name: "SupplyUsageLogs");

            migrationBuilder.DropTable(
                name: "LabelTemplates");

            migrationBuilder.DropTable(
                name: "PrintJobs");

            migrationBuilder.DropTable(
                name: "Supplies");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
