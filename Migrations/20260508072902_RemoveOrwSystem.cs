using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectTallify.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrwSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Scores_Scorers_ScorerId",
                table: "Scores");

            migrationBuilder.DropTable(
                name: "OrwPointRules");

            migrationBuilder.DropTable(
                name: "Scorers");

            migrationBuilder.DropIndex(
                name: "IX_Scores_ScorerId",
                table: "Scores");

            migrationBuilder.DropColumn(
                name: "ScorerId",
                table: "Scores");

            migrationBuilder.DropColumn(
                name: "PointingJson",
                table: "Events");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScorerId",
                table: "Scores",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PointingJson",
                table: "Events",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OrwPointRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RoundId = table.Column<int>(type: "int", nullable: false),
                    PointsPerBonus = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PointsPerCorrect = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PointsPerViolation = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PointsPerWrong = table.Column<decimal>(type: "decimal(65,30)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrwPointRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrwPointRules_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Scorers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    AssignedContestantIds = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Pin = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scorers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scorers_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Scores_ScorerId",
                table: "Scores",
                column: "ScorerId");

            migrationBuilder.CreateIndex(
                name: "IX_OrwPointRules_RoundId",
                table: "OrwPointRules",
                column: "RoundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scorers_EventId",
                table: "Scorers",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_Scores_Scorers_ScorerId",
                table: "Scores",
                column: "ScorerId",
                principalTable: "Scorers",
                principalColumn: "Id");
        }
    }
}
