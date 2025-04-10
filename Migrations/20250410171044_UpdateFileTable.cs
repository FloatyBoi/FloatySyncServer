using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FloatySyncServer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFileTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Files");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Files",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Files");

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Files",
                type: "TEXT",
                nullable: true);
        }
    }
}
