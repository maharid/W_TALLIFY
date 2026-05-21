using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectTallify.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRoundTypeFromRounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoundType",
                table: "Rounds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoundType",
                table: "Rounds",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
