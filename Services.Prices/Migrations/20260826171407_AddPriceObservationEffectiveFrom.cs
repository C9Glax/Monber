using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Prices.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceObservationEffectiveFrom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveFrom",
                table: "price_observations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "price_observations");
        }
    }
}
