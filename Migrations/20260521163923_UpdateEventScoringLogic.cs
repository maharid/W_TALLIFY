using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectTallify.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEventScoringLogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AveragingJson",
                table: "Events");

            migrationBuilder.RenameColumn(
                name: "EventType",
                table: "Events",
                newName: "ScoringLogic");

            // DATA MIGRATION: Convert old values to new ones
            migrationBuilder.Sql("UPDATE Events SET ScoringLogic = 'WA' WHERE ScoringLogic = 'criteria'");
            migrationBuilder.Sql("UPDATE Events SET ScoringLogic = 'PB' WHERE ScoringLogic = 'orw'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ScoringLogic",
                table: "Events",
                newName: "EventType");

            migrationBuilder.AddColumn<string>(
                name: "AveragingJson",
                table: "Events",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
