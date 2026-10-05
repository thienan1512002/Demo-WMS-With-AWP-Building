namespace DeliveryDemo.Infrastructure.Warehouse.Migrations;

internal static class WarehouseSql
{
    public const string Up = """
        CREATE FUNCTION warehouse_reject_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'Warehouse history is immutable; archive master data and post compensating transactions' USING ERRCODE = '23514';
        END $$;
        CREATE TRIGGER goods_no_delete BEFORE DELETE ON goods FOR EACH ROW EXECUTE FUNCTION warehouse_reject_mutation();
        CREATE TRIGGER warehouses_no_delete BEFORE DELETE ON warehouses FOR EACH ROW EXECUTE FUNCTION warehouse_reject_mutation();
        CREATE TRIGGER history_immutable BEFORE UPDATE OR DELETE ON stock_transactions FOR EACH ROW EXECUTE FUNCTION warehouse_reject_mutation();
        CREATE FUNCTION warehouse_balance_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF pg_trigger_depth() < 2 THEN
                RAISE EXCEPTION 'Balances may only be changed by stock postings' USING ERRCODE = '23514';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER balance_guard BEFORE INSERT OR UPDATE OR DELETE ON inventory_balances FOR EACH ROW EXECUTE FUNCTION warehouse_balance_guard();
        CREATE FUNCTION warehouse_post() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            PERFORM 1 FROM goods WHERE id = NEW.goods_id AND archived_at IS NULL FOR SHARE;
            IF NOT FOUND THEN
                RAISE EXCEPTION 'Goods missing or archived' USING ERRCODE = '23514';
            END IF;
            PERFORM 1 FROM warehouses WHERE id = NEW.warehouse_id AND archived_at IS NULL FOR SHARE;
            IF NOT FOUND THEN
                RAISE EXCEPTION 'Warehouse missing or archived' USING ERRCODE = '23514';
            END IF;
            NEW.created_at := CURRENT_TIMESTAMP;
            INSERT INTO inventory_balances(goods_id, warehouse_id, quantity, updated_at)
                VALUES (NEW.goods_id, NEW.warehouse_id, 0, CURRENT_TIMESTAMP)
                ON CONFLICT (goods_id, warehouse_id) DO NOTHING;
            UPDATE inventory_balances
                SET quantity = quantity + CASE WHEN NEW.type = 1 THEN NEW.quantity ELSE -NEW.quantity END,
                    updated_at = CURRENT_TIMESTAMP
                WHERE goods_id = NEW.goods_id AND warehouse_id = NEW.warehouse_id
                  AND (NEW.type = 1 OR quantity >= NEW.quantity);
            IF NOT FOUND THEN
                RAISE EXCEPTION 'Insufficient inventory' USING ERRCODE = '23514';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER stock_post BEFORE INSERT ON stock_transactions FOR EACH ROW EXECUTE FUNCTION warehouse_post();
        """;

    public const string Down = """
        DROP TRIGGER stock_post ON stock_transactions;
        DROP TRIGGER balance_guard ON inventory_balances;
        DROP TRIGGER history_immutable ON stock_transactions;
        DROP TRIGGER goods_no_delete ON goods;
        DROP TRIGGER warehouses_no_delete ON warehouses;
        DROP FUNCTION warehouse_post();
        DROP FUNCTION warehouse_balance_guard();
        DROP FUNCTION warehouse_reject_mutation();
        """;
}
