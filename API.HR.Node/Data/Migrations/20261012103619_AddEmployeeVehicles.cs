using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeVehicles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeVehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlateNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlateLetter = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Governorate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Vin = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManufacturingYear = table.Column<int>(type: "int", nullable: true),
                    IsAuthorized = table.Column<bool>(type: "bit", nullable: false),
                    AllowMainBuilding = table.Column<bool>(type: "bit", nullable: false),
                    AllowJadriya = table.Column<bool>(type: "bit", nullable: false),
                    AllowErbil = table.Column<bool>(type: "bit", nullable: false),
                    AllowMosul = table.Column<bool>(type: "bit", nullable: false),
                    AllowBasra = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeVehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeVehicles_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeVehicles_EmployeeId",
                table: "EmployeeVehicles",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeVehicles_IsAuthorized",
                table: "EmployeeVehicles",
                column: "IsAuthorized");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeVehicles_PlateNumber",
                table: "EmployeeVehicles",
                column: "PlateNumber");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeVehicles_Vin",
                table: "EmployeeVehicles",
                column: "Vin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeVehicles");
        }
    }
}
