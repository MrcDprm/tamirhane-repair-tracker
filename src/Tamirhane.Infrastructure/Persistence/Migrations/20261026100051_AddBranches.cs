using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tamirhane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "staff",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branches", x => x.id);
                    table.ForeignKey(
                        name: "fk_branches_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_staff_branch_id",
                table: "staff",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_branches_shop_id_created_at",
                table: "branches",
                columns: new[] { "shop_id", "created_at" });

            // Var olan atölyelere kayıttaki gibi ilk şube açılır, personeli ona bağlanır; yoksa FK eklenemez.
            migrationBuilder.Sql("""
                INSERT INTO branches (id, shop_id, name, address, created_at)
                SELECT gen_random_uuid(), s.id, CASE WHEN s.locale = 'en' THEN 'Main branch' ELSE 'Merkez' END, NULL, s.created_at
                FROM shops s;

                UPDATE staff SET branch_id = b.id FROM branches b WHERE b.shop_id = staff.shop_id;
                """);

            migrationBuilder.AddForeignKey(
                name: "fk_staff_branches_branch_id",
                table: "staff",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_staff_branches_branch_id",
                table: "staff");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropIndex(
                name: "ix_staff_branch_id",
                table: "staff");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "staff");
        }
    }
}
