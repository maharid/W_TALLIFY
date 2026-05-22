using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectTallify.Migrations
{
    /// <inheritdoc />
    public partial class UpdateContestantCodesToSequential : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retroactively fix contestant codes to be sequential (C001, C002, etc.) per event.
            // This is done via a SQL script that uses variables to track the index per EventId.
            migrationBuilder.Sql(@"
                SET @row_number = 0;
                SET @current_event_id = 0;

                UPDATE Contestants
                SET Code = (
                    SELECT CONCAT('C', LPAD(@row_number := IF(@current_event_id = EventId, @row_number + 1, 1), 3, '0'))
                    FROM (SELECT @current_event_id := EventId) AS init
                )
                ORDER BY EventId, Id;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
