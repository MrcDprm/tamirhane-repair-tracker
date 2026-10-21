using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tamirhane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShopIdToStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "shop_id",
                table: "stock_movements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "shop_id",
                table: "stock_levels",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Var olan stok kayıtları atölyesini parçadan alır; boş kalırsa hiçbir atölye göremez.
            migrationBuilder.Sql("UPDATE stock_levels l SET shop_id = p.shop_id FROM parts p WHERE p.id = l.part_id;");
            migrationBuilder.Sql("UPDATE stock_movements m SET shop_id = p.shop_id FROM parts p WHERE p.id = m.part_id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "shop_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "shop_id",
                table: "stock_levels");
        }
    }
}
