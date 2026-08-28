using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Prices.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceChecksTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "price_checks",
                columns: table => new
                {
                    StoreId = table.Column<long>(type: "INTEGER", nullable: false),
                    Product = table.Column<string>(type: "TEXT", nullable: false),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_checks", x => new { x.StoreId, x.Product });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_checks");
        }
    }
}
