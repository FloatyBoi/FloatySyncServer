using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FloatySyncServer.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDirectory",
                table: "Files",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDirectory",
                table: "Files");
        }
    }
}
