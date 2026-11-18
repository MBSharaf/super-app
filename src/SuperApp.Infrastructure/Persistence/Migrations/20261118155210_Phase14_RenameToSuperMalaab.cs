using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase14_RenameToSuperMalaab : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The app was renamed "Super Malaab" / "سوبر ملعب" (owner, 2026-11-18): every text already stored in the
            // database (content, settings, seeded names, notifications…) gets the new name. Values that would no
            // longer fit their column are left as they are.
            migrationBuilder.Sql(@"
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'UPDATE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' SET ' + QUOTENAME(c.name) + N' = ' + x.renamed
    + N' WHERE (' + QUOTENAME(c.name) + N' LIKE N''%Super App%'' OR ' + QUOTENAME(c.name) + N' LIKE N''%سوبر آب%'')'
    + CASE WHEN c.max_length = -1 THEN N'' ELSE N' AND LEN(' + x.renamed + N') <= '
        + CAST(CASE WHEN ty.name IN (N'nvarchar', N'nchar') THEN c.max_length / 2 ELSE c.max_length END AS nvarchar(10)) END
    + N';' + NCHAR(10)
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
CROSS APPLY (SELECT N'REPLACE(REPLACE(REPLACE(' + QUOTENAME(c.name)
    + N', N''سوبر آب'', N''سوبر ملعب''), N''Super App'', N''Super Malaab''), N''SUPER APP'', N''SUPER MALAAB'')' AS renamed) x
WHERE ty.name IN (N'nvarchar', N'varchar', N'nchar', N'char') AND c.is_computed = 0 AND t.is_ms_shipped = 0
  AND t.name <> N'__EFMigrationsHistory';
EXEC sp_executesql @sql;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the old name is not put back into stored texts
        }
    }
}
