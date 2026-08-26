using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Prices.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceObservationSourceUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "price_observations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "price_observations");
        }
    }
}
