using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectTallify.Migrations
{
    /// <inheritdoc />
    public partial class FixLegacyScoringLogicValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Events SET ScoringLogic = 'WA' WHERE ScoringLogic = 'averaging'");
            migrationBuilder.Sql("UPDATE Events SET ScoringLogic = 'PB' WHERE ScoringLogic = 'pointing'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
