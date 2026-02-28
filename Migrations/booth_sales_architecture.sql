-- ═══════════════════════════════════════════════════════════════════════
-- Migration: Booth Sales Data Architecture Fix
-- Description: Add missing columns to booth_sales table and migrate
--              existing booth sale data from the Orders table.
--
-- Booth sales should be stored in the booth_sales table (not Orders)
-- because booth inventory comes from the troop, not Girl Scout personal
-- inventory. This migration adds the columns needed to fully support
-- booth sale transactions in the booth_sales table.
-- ═══════════════════════════════════════════════════════════════════════

-- Step 1: Add missing columns to booth_sales table
ALTER TABLE booth_sales ADD COLUMN IF NOT EXISTS booth_session_id uuid REFERENCES booth_sessions(id);
ALTER TABLE booth_sales ADD COLUMN IF NOT EXISTS sale_group_id uuid;
ALTER TABLE booth_sales ADD COLUMN IF NOT EXISTS girl_scout_id uuid REFERENCES girl_scouts(id);
ALTER TABLE booth_sales ADD COLUMN IF NOT EXISTS payment_method text;
ALTER TABLE booth_sales ADD COLUMN IF NOT EXISTS unit_price numeric(10,2);

-- Step 2: Create indexes for common queries
CREATE INDEX IF NOT EXISTS idx_booth_sales_session_id ON booth_sales(booth_session_id);
CREATE INDEX IF NOT EXISTS idx_booth_sales_sale_group_id ON booth_sales(sale_group_id);
CREATE INDEX IF NOT EXISTS idx_booth_sales_booth_date ON booth_sales(booth_date);

-- Step 3: Migrate existing booth sale data from Orders + OrderLineItems to booth_sales
-- Each OrderLineItem for a Booth Sale order becomes a row in booth_sales.
-- The order's ID is used as the sale_group_id to group product lines from the same transaction.
INSERT INTO booth_sales (
    id,
    booth_session_id,
    sale_group_id,
    booth_date,
    location,
    product_id,
    quantity_boxes,
    unit_price,
    from_personal_inventory,
    girl_scout_id,
    payment_method,
    notes,
    created_at,
    updated_at
)
SELECT
    gen_random_uuid(),
    o.booth_session_id,
    o.id,  -- use the order ID as the sale_group_id
    COALESCE(o.ordered_at::date, CURRENT_DATE),
    COALESCE(bs.location, ''),
    oli.product_id,
    oli.quantity_boxes,
    oli.unit_price,
    CASE WHEN oli.inventory_source = 'Personal' THEN true ELSE false END,
    o.girl_scout_id,
    o.payment_method,
    o.notes,
    o.created_at,
    o.updated_at
FROM orders o
JOIN order_line_items oli ON oli.order_id = o.id
LEFT JOIN booth_sessions bs ON bs.id = o.booth_session_id
WHERE o.order_type = 'Booth Sale'
  AND NOT EXISTS (
    -- Prevent duplicate migration: skip if booth_sales already has data for this session
    SELECT 1 FROM booth_sales existing
    WHERE existing.booth_session_id = o.booth_session_id
      AND existing.sale_group_id IS NOT NULL
  );

-- NOTE: The migrated data from Orders is NOT automatically deleted.
-- After verifying the migration was successful, you can optionally clean up
-- the old booth sale orders with:
--
-- DELETE FROM order_line_items WHERE order_id IN (SELECT id FROM orders WHERE order_type = 'Booth Sale');
-- DELETE FROM orders WHERE order_type = 'Booth Sale';
--
-- Do NOT run the cleanup until you've confirmed the app is working correctly
-- with the new booth_sales data.
