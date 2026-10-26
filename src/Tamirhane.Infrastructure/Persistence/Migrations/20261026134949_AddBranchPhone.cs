using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tamirhane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "branches",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "phone",
                table: "branches");
        }
    }
}
