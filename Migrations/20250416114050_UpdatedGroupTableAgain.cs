using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FloatySyncServer.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedGroupTableAgain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Files");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Files",
                type: "INTEGER",
                nullable: true);
        }
    }
}
