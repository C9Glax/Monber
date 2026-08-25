using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.POI.Migrations
{
    /// <inheritdoc />
    public partial class FixVersionsPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_versions",
                table: "versions");

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "versions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_versions",
                table: "versions",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_versions",
                table: "versions");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "versions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_versions",
                table: "versions",
                column: "Generator");
        }
    }
}
