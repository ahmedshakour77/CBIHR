using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePassports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeePassports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PassportNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PassportType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IssuingCountry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IssuingAuthority = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NationalIdNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FullNameAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FullNameEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MotherName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PlaceOfBirth = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RawMrz = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PassportImageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PassportImageFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PassportImageContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BiometricPhotoKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BiometricPhotoFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BiometricPhotoContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePassports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePassports_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePassports_EmployeeId",
                table: "EmployeePassports",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePassports_NationalIdNumber",
                table: "EmployeePassports",
                column: "NationalIdNumber");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePassports_PassportNumber",
                table: "EmployeePassports",
                column: "PassportNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePassports");
        }
    }
}
