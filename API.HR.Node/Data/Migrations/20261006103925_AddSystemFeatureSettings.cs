using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemFeatureSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemFeatureSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnableMyFeedback = table.Column<bool>(type: "bit", nullable: false),
                    EnableFeedbackFab = table.Column<bool>(type: "bit", nullable: false),
                    AllowIdeas = table.Column<bool>(type: "bit", nullable: false),
                    AllowNotes = table.Column<bool>(type: "bit", nullable: false),
                    AllowInquiries = table.Column<bool>(type: "bit", nullable: false),
                    AllowComplaints = table.Column<bool>(type: "bit", nullable: false),
                    AllowOthers = table.Column<bool>(type: "bit", nullable: false),
                    AllowAttachments = table.Column<bool>(type: "bit", nullable: false),
                    DisabledMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemFeatureSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SystemFeatureSettings",
                columns: new[] { "Id", "AllowAttachments", "AllowComplaints", "AllowIdeas", "AllowInquiries", "AllowNotes", "AllowOthers", "DisabledMessage", "EnableFeedbackFab", "EnableMyFeedback", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { 1, true, true, true, true, true, true, "خدمة تقديم الأفكار والمقترحات متوقفة مؤقتاً بقرار الإدارة.", true, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemFeatureSettings");
        }
    }
}
