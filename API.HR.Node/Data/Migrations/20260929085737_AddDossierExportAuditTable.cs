using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDossierExportAuditTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DossierExportAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VerificationToken = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    EmployeeIDNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FormationName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    JobTitleName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SnapshotDataDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExportedByUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExportedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExportedFromIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerificationCount = table.Column<int>(type: "int", nullable: false),
                    LastVerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DossierExportAudits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DossierExportAudits_CreatedAtUtc",
                table: "DossierExportAudits",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DossierExportAudits_EmployeeIDNumber",
                table: "DossierExportAudits",
                column: "EmployeeIDNumber");

            migrationBuilder.CreateIndex(
                name: "IX_DossierExportAudits_VerificationToken",
                table: "DossierExportAudits",
                column: "VerificationToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DossierExportAudits");
        }
    }
}
