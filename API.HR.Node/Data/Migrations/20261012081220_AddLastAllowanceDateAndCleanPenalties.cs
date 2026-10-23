using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLastAllowanceDateAndCleanPenalties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobTitles_JobFamily' AND object_id = OBJECT_ID('JobTitles'))
    DROP INDEX [IX_JobTitles_JobFamily] ON [JobTitles];

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'JobTitles' AND COLUMN_NAME = 'GradeName')
    ALTER TABLE [JobTitles] DROP COLUMN [GradeName];

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'JobTitles' AND COLUMN_NAME = 'JobFamily')
    ALTER TABLE [JobTitles] DROP COLUMN [JobFamily];

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'EmployeePenalties' AND COLUMN_NAME = 'Directorate')
    ALTER TABLE [EmployeePenalties] DROP COLUMN [Directorate];

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'EmployeePenalties' AND COLUMN_NAME = 'JobTitle')
    ALTER TABLE [EmployeePenalties] DROP COLUMN [JobTitle];

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'EmployeePenalties' AND COLUMN_NAME = 'PenaltyName')
    ALTER TABLE [EmployeePenalties] DROP COLUMN [PenaltyName];

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'JobFamilies')
BEGIN
    CREATE TABLE [JobFamilies] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Name] nvarchar(200) NOT NULL,
        [Code] nvarchar(50) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_JobFamilies] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_JobFamilies_Name] ON [JobFamilies] ([Name]);
    CREATE INDEX [IX_JobFamilies_Code] ON [JobFamilies] ([Code]);
END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'JobTitles' AND COLUMN_NAME = 'JobFamilyId')
    ALTER TABLE [JobTitles] ADD [JobFamilyId] int NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'LastAllowanceDate')
    ALTER TABLE [Employees] ADD [LastAllowanceDate] date NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'ArchiveReason')
    ALTER TABLE [Employees] ADD [ArchiveReason] nvarchar(500) NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'ArchivedAt')
    ALTER TABLE [Employees] ADD [ArchivedAt] datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'ArchivedBy')
    ALTER TABLE [Employees] ADD [ArchivedBy] nvarchar(100) NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'IsArchived')
    ALTER TABLE [Employees] ADD [IsArchived] bit NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobTitles_JobFamilyId' AND object_id = OBJECT_ID('JobTitles'))
    CREATE INDEX [IX_JobTitles_JobFamilyId] ON [JobTitles] ([JobFamilyId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_IsArchived' AND object_id = OBJECT_ID('Employees'))
    CREATE INDEX [IX_Employees_IsArchived] ON [Employees] ([IsArchived]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_JobTitles_JobFamilies_JobFamilyId')
    ALTER TABLE [JobTitles] ADD CONSTRAINT [FK_JobTitles_JobFamilies_JobFamilyId] 
    FOREIGN KEY ([JobFamilyId]) REFERENCES [JobFamilies] ([Id]) ON DELETE SET NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobTitles_JobFamilies_JobFamilyId",
                table: "JobTitles");

            migrationBuilder.DropTable(
                name: "JobFamilies");

            migrationBuilder.DropIndex(
                name: "IX_JobTitles_JobFamilyId",
                table: "JobTitles");

            migrationBuilder.DropIndex(
                name: "IX_Employees_IsArchived",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "JobFamilyId",
                table: "JobTitles");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LastAllowanceDate",
                table: "Employees");

            migrationBuilder.AddColumn<string>(
                name: "GradeName",
                table: "JobTitles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobFamily",
                table: "JobTitles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Directorate",
                table: "EmployeePenalties",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "EmployeePenalties",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PenaltyName",
                table: "EmployeePenalties",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobTitles_JobFamily",
                table: "JobTitles",
                column: "JobFamily");
        }
    }
}
