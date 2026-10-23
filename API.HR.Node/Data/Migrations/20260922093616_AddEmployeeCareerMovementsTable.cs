using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeCareerMovementsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeCareerMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    MovementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FormationName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CountForAllowance = table.Column<bool>(type: "bit", nullable: false),
                    CountForPromotion = table.Column<bool>(type: "bit", nullable: false),
                    CountForPension = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCareerMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCareerMovements_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCareerMovements_EmployeeId_MovementDate",
                table: "EmployeeCareerMovements",
                columns: new[] { "EmployeeId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCareerMovements_MovementDate",
                table: "EmployeeCareerMovements",
                column: "MovementDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeCareerMovements");
        }
    }
}
