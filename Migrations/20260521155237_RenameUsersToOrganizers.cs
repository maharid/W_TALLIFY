using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectTallify.Migrations
{
    /// <inheritdoc />
    public partial class RenameUsersToOrganizers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Users_UserId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationLogs_Users_UserId",
                table: "NotificationLogs");

            // RENAME TABLE INSTEAD OF DROP/CREATE
            migrationBuilder.RenameTable(
                name: "Users",
                newName: "Organizers");

            // DROP ROLE COLUMN
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Organizers");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "NotificationLogs",
                newName: "OrganizerId");

            migrationBuilder.RenameIndex(
                name: "IX_NotificationLogs_UserId",
                table: "NotificationLogs",
                newName: "IX_NotificationLogs_OrganizerId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Events",
                newName: "OrganizerId");

            migrationBuilder.RenameIndex(
                name: "IX_Events_UserId",
                table: "Events",
                newName: "IX_Events_OrganizerId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "AuditLogs",
                newName: "OrganizerId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                newName: "IX_AuditLogs_OrganizerId");

            migrationBuilder.AddColumn<string>(
                name: "ActionUrl",
                table: "NotificationLogs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Organizers_OrganizerId",
                table: "AuditLogs",
                column: "OrganizerId",
                principalTable: "Organizers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Organizers_OrganizerId",
                table: "Events",
                column: "OrganizerId",
                principalTable: "Organizers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationLogs_Organizers_OrganizerId",
                table: "NotificationLogs",
                column: "OrganizerId",
                principalTable: "Organizers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Organizers_OrganizerId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Organizers_OrganizerId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationLogs_Organizers_OrganizerId",
                table: "NotificationLogs");

            // REVERT RENAME
            migrationBuilder.RenameTable(
                name: "Organizers",
                newName: "Users");

            // ADD ROLE COLUMN BACK
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "longtext",
                nullable: false,
                defaultValue: "Organizer")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.DropColumn(
                name: "ActionUrl",
                table: "NotificationLogs");

            migrationBuilder.RenameColumn(
                name: "OrganizerId",
                table: "NotificationLogs",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_NotificationLogs_OrganizerId",
                table: "NotificationLogs",
                newName: "IX_NotificationLogs_UserId");

            migrationBuilder.RenameColumn(
                name: "OrganizerId",
                table: "Events",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Events_OrganizerId",
                table: "Events",
                newName: "IX_Events_UserId");

            migrationBuilder.RenameColumn(
                name: "OrganizerId",
                table: "AuditLogs",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_OrganizerId",
                table: "AuditLogs",
                newName: "IX_AuditLogs_UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                table: "AuditLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Users_UserId",
                table: "Events",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationLogs_Users_UserId",
                table: "NotificationLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
