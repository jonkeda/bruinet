using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bruinet.Samples.Sql.App.Data.Migrations
{
    /// <summary>A view and a stored procedure, as a legacy schema has them: their tables are named only in their bodies.</summary>
    public partial class AddKindCountsAndVisitCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE VIEW [dbo].[KindCounts] AS
                SELECT [Kind], COUNT(*) AS [Places] FROM [dbo].[Places] GROUP BY [Kind]
                """);
            migrationBuilder.Sql("""
                CREATE PROCEDURE [dbo].[VisitCount] @PlaceId uniqueidentifier AS
                SELECT COUNT(*) AS [Value] FROM [dbo].[Visits] WHERE [PlaceId] = @PlaceId
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE [dbo].[VisitCount]");
            migrationBuilder.Sql("DROP VIEW [dbo].[KindCounts]");
        }
    }
}
