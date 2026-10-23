using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeMartyrsAndLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MartyrdomTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MartyrdomTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MartyrKinships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Degree = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MartyrKinships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeMartyrs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    EmployeeIdNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MartyrName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    MartyrdomTypeId = table.Column<int>(type: "int", nullable: true),
                    KinshipId = table.Column<int>(type: "int", nullable: true),
                    DecisionNumber = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeMartyrs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeMartyrs_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeMartyrs_MartyrKinships_KinshipId",
                        column: x => x.KinshipId,
                        principalTable: "MartyrKinships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EmployeeMartyrs_MartyrdomTypes_MartyrdomTypeId",
                        column: x => x.MartyrdomTypeId,
                        principalTable: "MartyrdomTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeMartyrs_EmployeeId",
                table: "EmployeeMartyrs",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeMartyrs_EmployeeIdNumber",
                table: "EmployeeMartyrs",
                column: "EmployeeIdNumber");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeMartyrs_KinshipId",
                table: "EmployeeMartyrs",
                column: "KinshipId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeMartyrs_MartyrdomTypeId",
                table: "EmployeeMartyrs",
                column: "MartyrdomTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MartyrdomTypes_Name",
                table: "MartyrdomTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MartyrKinships_Name",
                table: "MartyrKinships",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeMartyrs");

            migrationBuilder.DropTable(
                name: "MartyrKinships");

            migrationBuilder.DropTable(
                name: "MartyrdomTypes");
        }
    }
}
