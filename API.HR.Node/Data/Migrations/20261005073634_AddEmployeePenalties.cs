using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePenalties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PenaltyTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DelayMonths = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PenaltyTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePenalties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PenaltyTypeId = table.Column<int>(type: "int", nullable: true),
                    PenaltyName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    PenaltyDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Directorate = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePenalties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePenalties_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeePenalties_PenaltyTypes_PenaltyTypeId",
                        column: x => x.PenaltyTypeId,
                        principalTable: "PenaltyTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePenalties_EmployeeId_PenaltyDate",
                table: "EmployeePenalties",
                columns: new[] { "EmployeeId", "PenaltyDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePenalties_PenaltyDate",
                table: "EmployeePenalties",
                column: "PenaltyDate");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePenalties_PenaltyTypeId",
                table: "EmployeePenalties",
                column: "PenaltyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyTypes_Name",
                table: "PenaltyTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePenalties");

            migrationBuilder.DropTable(
                name: "PenaltyTypes");
        }
    }
}
