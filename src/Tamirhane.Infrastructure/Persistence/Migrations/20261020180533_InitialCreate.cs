using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tamirhane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    kvkk_consent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cost_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    sale_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    critical_level = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    change = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_order_sequences",
                columns: table => new
                {
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_order_sequences", x => new { x.shop_id, x.year });
                });

            migrationBuilder.CreateTable(
                name: "work_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tracking_code = table.Column<string>(type: "character(12)", fixedLength: true, maxLength: 12, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    complaint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    condition = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    accessories = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    passcode = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    parts_consumed = table.Column<bool>(type: "boolean", nullable: false),
                    quote_decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    quote_decided_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    quote_decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    quote_decided_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    diagnosis_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    balance_at_delivery = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    warranty_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    warranty_of_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    brand = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    serial = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    imei = table.Column<string>(type: "character(15)", fixedLength: true, maxLength: 15, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_devices_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_levels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_levels", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_levels_parts_part_id",
                        column: x => x.part_id,
                        principalTable: "parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    part_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    discount_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_quote_lines_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "status_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    to = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_status_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_status_changes_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    visible_to_customer = table.Column<bool>(type: "boolean", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_order_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_order_notes_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customers_shop_id_phone",
                table: "customers",
                columns: new[] { "shop_id", "phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_devices_customer_id",
                table: "devices",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_devices_shop_id_imei",
                table: "devices",
                columns: new[] { "shop_id", "imei" });

            migrationBuilder.CreateIndex(
                name: "ix_parts_shop_id_sku",
                table: "parts",
                columns: new[] { "shop_id", "sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_work_order_id",
                table: "payments",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_lines_part_id",
                table: "quote_lines",
                column: "part_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_lines_work_order_id",
                table: "quote_lines",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_status_changes_work_order_id_at",
                table: "status_changes",
                columns: new[] { "work_order_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_levels_part_id_branch_id",
                table: "stock_levels",
                columns: new[] { "part_id", "branch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_part_id_at",
                table: "stock_movements",
                columns: new[] { "part_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_work_order_id",
                table: "stock_movements",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_notes_work_order_id",
                table: "work_order_notes",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_customer_id",
                table: "work_orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_shop_id_number",
                table: "work_orders",
                columns: new[] { "shop_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_shop_id_status",
                table: "work_orders",
                columns: new[] { "shop_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_tracking_code",
                table: "work_orders",
                column: "tracking_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "quote_lines");

            migrationBuilder.DropTable(
                name: "status_changes");

            migrationBuilder.DropTable(
                name: "stock_levels");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "work_order_notes");

            migrationBuilder.DropTable(
                name: "work_order_sequences");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "parts");

            migrationBuilder.DropTable(
                name: "work_orders");
        }
    }
}
