using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryDemo.Infrastructure.Warehouse.Migrations
{
    /// <inheritdoc />
    public partial class InitialWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "goods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goods", x => x.id);
                    table.CheckConstraint("ck_goods_text", "btrim(code) <> '' AND btrim(name) <> ''");
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_warehouses", x => x.id);
                    table.CheckConstraint("ck_warehouses_text", "btrim(code) <> '' AND btrim(name) <> ''");
                });

            migrationBuilder.CreateTable(
                name: "inventory_balances",
                columns: table => new
                {
                    goods_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_balances", x => new { x.goods_id, x.warehouse_id });
                    table.CheckConstraint("ck_balance_quantity", "quantity >= 0 AND quantity < 100000000000000");
                    table.ForeignKey(
                        name: "fk_inventory_balances_goods_goods_id",
                        column: x => x.goods_id,
                        principalTable: "goods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_balances_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_transactions", x => x.id);
                    table.CheckConstraint("ck_transaction_actor", "btrim(actor) <> ''");
                    table.CheckConstraint("ck_transaction_quantity", "quantity > 0 AND quantity < 100000000000000");
                    table.CheckConstraint("ck_transaction_type", "type IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_stock_transactions_goods_goods_id",
                        column: x => x.goods_id,
                        principalTable: "goods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transactions_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_goods_code",
                table: "goods",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_balances_warehouse_id",
                table: "inventory_balances",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_goods_id_warehouse_id_occurred_at",
                table: "stock_transactions",
                columns: new[] { "goods_id", "warehouse_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_warehouse_id_occurred_at",
                table: "stock_transactions",
                columns: new[] { "warehouse_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_code",
                table: "warehouses",
                column: "code",
                unique: true);
            migrationBuilder.Sql(WarehouseSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(WarehouseSql.Down);
            migrationBuilder.DropTable(
                name: "inventory_balances");

            migrationBuilder.DropTable(
                name: "stock_transactions");

            migrationBuilder.DropTable(
                name: "goods");

            migrationBuilder.DropTable(
                name: "warehouses");
        }
    }
}
