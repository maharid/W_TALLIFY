using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectTallify.Migrations
{
    /// <inheritdoc />
    public partial class UpdateScoringLogicToFullWords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Events SET ScoringLogic = 'WeightedAverage' WHERE ScoringLogic = 'WA'");
            migrationBuilder.Sql("UPDATE Events SET ScoringLogic = 'PointBased' WHERE ScoringLogic = 'PB'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
