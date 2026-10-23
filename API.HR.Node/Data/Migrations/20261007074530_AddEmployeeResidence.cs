using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeResidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Governorates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameKu = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Governorates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeResidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    EmployeeIdNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GovernorateId = table.Column<int>(type: "int", nullable: true),
                    InformationOffice = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    FamilyHeadName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    HousingType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Mahalla = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Zuqaq = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Dar = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ComplexName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BuildingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApartmentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FormNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FormDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LocationUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CardFrontKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CardFrontFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CardFrontContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CardBackKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CardBackFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CardBackContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeResidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeResidences_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeResidences_Governorates_GovernorateId",
                        column: x => x.GovernorateId,
                        principalTable: "Governorates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "Governorates",
                columns: new[] { "Id", "DisplayOrder", "Name", "NameKu" },
                values: new object[,]
                {
                    { 1, 1, "بغداد", "بەغدا" },
                    { 2, 2, "البصرة", "بەسرە" },
                    { 3, 3, "نينوى", "نەینەوا" },
                    { 4, 4, "أربيل", "هەولێر" },
                    { 5, 5, "النجف الأشرف", "نەجەف" },
                    { 6, 6, "كربلاء المقدسة", "کەربەلا" },
                    { 7, 7, "كركوك", "کەرکووک" },
                    { 8, 8, "الأنبار", "ئەنبار" },
                    { 9, 9, "بابل", "بابل" },
                    { 10, 10, "السليمانية", "سلێمانی" },
                    { 11, 11, "دهوك", "دهۆک" },
                    { 12, 12, "ديالى", "دیالە" },
                    { 13, 13, "صلاح الدين", "سەڵاحەدین" },
                    { 14, 14, "واسط", "واسیت" },
                    { 15, 15, "ميسان", "مەیسان" },
                    { 16, 16, "ذي قار", "زیقار" },
                    { 17, 17, "المثنى", "موسەننا" },
                    { 18, 18, "القادسية", "قادسیە" },
                    { 19, 19, "حلبجة", "هەڵەبجە" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeResidences_EmployeeId",
                table: "EmployeeResidences",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeResidences_EmployeeIdNumber",
                table: "EmployeeResidences",
                column: "EmployeeIdNumber");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeResidences_FromDate",
                table: "EmployeeResidences",
                column: "FromDate");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeResidences_GovernorateId",
                table: "EmployeeResidences",
                column: "GovernorateId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeResidences_ToDate",
                table: "EmployeeResidences",
                column: "ToDate");

            migrationBuilder.CreateIndex(
                name: "IX_Governorates_Name",
                table: "Governorates",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeResidences");

            migrationBuilder.DropTable(
                name: "Governorates");
        }
    }
}
