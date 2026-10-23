using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFormationIdToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'FormationId'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [FormationId] int NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AspNetUsers_FormationId' AND object_id = OBJECT_ID('AspNetUsers'))
BEGIN
    CREATE INDEX [IX_AspNetUsers_FormationId] ON [AspNetUsers] ([FormationId]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AspNetUsers_Formations_FormationId')
BEGIN
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Formations')
    BEGIN
        ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_Formations_FormationId] 
        FOREIGN KEY ([FormationId]) REFERENCES [Formations] ([Id]) ON DELETE SET NULL;
    END;
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AspNetUsers_Formations_FormationId')
    ALTER TABLE [AspNetUsers] DROP CONSTRAINT [FK_AspNetUsers_Formations_FormationId];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AspNetUsers_FormationId' AND object_id = OBJECT_ID('AspNetUsers'))
    DROP INDEX [IX_AspNetUsers_FormationId] ON [AspNetUsers];

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'FormationId')
    ALTER TABLE [AspNetUsers] DROP COLUMN [FormationId];
");
        }
    }
}
