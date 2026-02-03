-- ============================================================
-- MIGRATION: Sync Supabase DB with ASP.NET entity models
-- Safe to run multiple times (uses IF NOT EXISTS / ADD COLUMN IF NOT EXISTS)
-- Run this in Supabase SQL Editor (Dashboard > SQL Editor > New query)
-- ============================================================

-- =========================
-- 1. Create missing tables
-- =========================

-- girl_scouts table (referenced by orders and inventory_batches)
CREATE TABLE IF NOT EXISTS girl_scouts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    first_name VARCHAR(100) NOT NULL DEFAULT '',
    last_name VARCHAR(100) NOT NULL DEFAULT '',
    troop_number VARCHAR(50)
);

-- inventory_batches table (groups inventory receipts into pickup batches)
CREATE TABLE IF NOT EXISTS inventory_batches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    status VARCHAR(50),
    batch_type VARCHAR(50),
    pickup_date DATE,
    notes TEXT,
    girl_scout_id UUID REFERENCES girl_scouts(id),
    total_boxes INT,
    total_cases INT
);

-- inventory_returns table (track product returned to troop)
CREATE TABLE IF NOT EXISTS inventory_returns (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id UUID REFERENCES products(id),
    quantity_boxes INT NOT NULL DEFAULT 0,
    quantity_cases INT NOT NULL DEFAULT 0,
    returned_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    reason VARCHAR(200),
    notes TEXT
);

-- ==============================
-- 2. Add missing columns: orders
-- ==============================
-- payment_method, is_online_paid, status, created_at, updated_at already exist in DB.
ALTER TABLE orders ADD COLUMN IF NOT EXISTS total_price DECIMAL;
ALTER TABLE orders ADD COLUMN IF NOT EXISTS total_qty INT;
ALTER TABLE orders ADD COLUMN IF NOT EXISTS paid_amount DECIMAL;
ALTER TABLE orders ADD COLUMN IF NOT EXISTS delivery_date DATE;
ALTER TABLE orders ADD COLUMN IF NOT EXISTS girl_scout_id UUID REFERENCES girl_scouts(id);

-- ====================================
-- 3. Add missing columns: paybacks
-- ====================================
ALTER TABLE paybacks ADD COLUMN IF NOT EXISTS order_id UUID REFERENCES orders(id);
ALTER TABLE paybacks ADD COLUMN IF NOT EXISTS customer_id UUID REFERENCES customers(id);

-- ==========================================
-- 4. Add missing columns: inventory_receipts
-- ==========================================
ALTER TABLE inventory_receipts ADD COLUMN IF NOT EXISTS inventory_batch_id UUID REFERENCES inventory_batches(id);
ALTER TABLE inventory_receipts ADD COLUMN IF NOT EXISTS notes TEXT;

-- ==========================================
-- 5. Add missing columns: order_line_items
-- ==========================================
ALTER TABLE order_line_items ADD COLUMN IF NOT EXISTS inventory_source VARCHAR(20) DEFAULT 'Personal';

-- =====================================
-- 6. Add missing columns: products
-- =====================================
ALTER TABLE products ADD COLUMN IF NOT EXISTS image_path TEXT;
ALTER TABLE products ADD COLUMN IF NOT EXISTS category TEXT;
ALTER TABLE products ADD COLUMN IF NOT EXISTS vendor TEXT;
ALTER TABLE products ADD COLUMN IF NOT EXISTS cost DECIMAL;
ALTER TABLE products ADD COLUMN IF NOT EXISTS reward DECIMAL;
ALTER TABLE products ADD COLUMN IF NOT EXISTS barcode TEXT;

-- =====================================
-- 7. Add missing columns: customers
-- =====================================
ALTER TABLE customers ADD COLUMN IF NOT EXISTS notes TEXT;

-- ============================================================
-- DONE. All tables and columns should now match the app models.
-- ============================================================
