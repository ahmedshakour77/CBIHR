using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeFeedbacksTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FeedbackType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WordCount = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SubmitterName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SubmitterEmail = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SubmitterJobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmitterDepartment = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AttachmentStorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AttachmentSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    AttachmentContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdminNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RespondedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RespondedByUserName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeFeedbacks_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFeedbacks_CreatedAt",
                table: "EmployeeFeedbacks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFeedbacks_EmployeeId",
                table: "EmployeeFeedbacks",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFeedbacks_FeedbackType",
                table: "EmployeeFeedbacks",
                column: "FeedbackType");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFeedbacks_ReferenceNumber",
                table: "EmployeeFeedbacks",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFeedbacks_Status",
                table: "EmployeeFeedbacks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeFeedbacks");
        }
    }
}
