using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Prices.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "price_observations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StoreId = table.Column<long>(type: "INTEGER", nullable: false),
                    Product = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_observations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "price_sync_versions",
                columns: table => new
                {
                    Brand = table.Column<string>(type: "TEXT", nullable: false),
                    LastRefreshedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_sync_versions", x => x.Brand);
                });

            migrationBuilder.CreateTable(
                name: "store_external_ids",
                columns: table => new
                {
                    Brand = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalStoreId = table.Column<string>(type: "TEXT", nullable: false),
                    StoreId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_external_ids", x => new { x.Brand, x.ExternalStoreId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_price_observations_StoreId_Product_FetchedAt",
                table: "price_observations",
                columns: new[] { "StoreId", "Product", "FetchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_store_external_ids_StoreId",
                table: "store_external_ids",
                column: "StoreId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_observations");

            migrationBuilder.DropTable(
                name: "price_sync_versions");

            migrationBuilder.DropTable(
                name: "store_external_ids");
        }
    }
}
