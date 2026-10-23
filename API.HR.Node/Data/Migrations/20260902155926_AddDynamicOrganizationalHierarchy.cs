using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicOrganizationalHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdministrativeUnitTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NameKu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HierarchyLevel = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministrativeUnitTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdministrativeUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NameKu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UnitTypeId = table.Column<int>(type: "int", nullable: false),
                    ParentUnitId = table.Column<int>(type: "int", nullable: true),
                    ManagerTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ManagerEmployeeId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministrativeUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdministrativeUnits_AdministrativeUnitTypes_UnitTypeId",
                        column: x => x.UnitTypeId,
                        principalTable: "AdministrativeUnitTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdministrativeUnits_AdministrativeUnits_ParentUnitId",
                        column: x => x.ParentUnitId,
                        principalTable: "AdministrativeUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdministrativeUnitClosures",
                columns: table => new
                {
                    AncestorId = table.Column<int>(type: "int", nullable: false),
                    DescendantId = table.Column<int>(type: "int", nullable: false),
                    Depth = table.Column<int>(type: "int", nullable: false),
                    LineagePath = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministrativeUnitClosures", x => new { x.AncestorId, x.DescendantId });
                    table.ForeignKey(
                        name: "FK_AdministrativeUnitClosures_AdministrativeUnits_AncestorId",
                        column: x => x.AncestorId,
                        principalTable: "AdministrativeUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdministrativeUnitClosures_AdministrativeUnits_DescendantId",
                        column: x => x.DescendantId,
                        principalTable: "AdministrativeUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeUnitClosures_Depth",
                table: "AdministrativeUnitClosures",
                column: "Depth");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeUnitClosures_DescendantId",
                table: "AdministrativeUnitClosures",
                column: "DescendantId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeUnitClosures_LineagePath",
                table: "AdministrativeUnitClosures",
                column: "LineagePath");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeUnits_Code",
                table: "AdministrativeUnits",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeUnits_Name",
                table: "AdministrativeUnits",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeUnits_ParentUnitId",
                table: "AdministrativeUnits",
                column: "ParentUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeUnits_UnitTypeId",
                table: "AdministrativeUnits",
                column: "UnitTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeUnitTypes_Code",
                table: "AdministrativeUnitTypes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeUnitTypes_HierarchyLevel",
                table: "AdministrativeUnitTypes",
                column: "HierarchyLevel");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeUnitTypes_Name",
                table: "AdministrativeUnitTypes",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministrativeUnitClosures");

            migrationBuilder.DropTable(
                name: "AdministrativeUnits");

            migrationBuilder.DropTable(
                name: "AdministrativeUnitTypes");
        }
    }
}
