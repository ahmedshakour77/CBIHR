using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeNationalCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Formations_FormationId",
                table: "AspNetUsers");

            migrationBuilder.CreateTable(
                name: "EmployeeNationalCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NationalIdNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FullNameAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FirstNameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LastNameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FullNameEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IssuingState = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RawMrz = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BiometricPhotoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FrontImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BackImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsPassiveAuthenticationValid = table.Column<bool>(type: "bit", nullable: false),
                    VerificationTimestamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SodHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CertificateIssuer = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeNationalCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeNationalCards_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeNationalCards_DocumentNumber",
                table: "EmployeeNationalCards",
                column: "DocumentNumber");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeNationalCards_EmployeeId",
                table: "EmployeeNationalCards",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeNationalCards_NationalIdNumber",
                table: "EmployeeNationalCards",
                column: "NationalIdNumber");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Formations_FormationId",
                table: "AspNetUsers",
                column: "FormationId",
                principalTable: "Formations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Formations_FormationId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "EmployeeNationalCards");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Formations_FormationId",
                table: "AspNetUsers",
                column: "FormationId",
                principalTable: "Formations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
