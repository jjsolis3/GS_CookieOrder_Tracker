-- Migration: Add sort_order column to products table
-- Run this in Supabase SQL Editor

-- Add sort_order column to products table (default 0, lower values appear first)
ALTER TABLE products
ADD COLUMN IF NOT EXISTS sort_order integer NOT NULL DEFAULT 0;

-- Optional: Add an index if you frequently query/sort by this column
CREATE INDEX IF NOT EXISTS idx_products_sort_order ON products(sort_order, name);

-- Verify the column was added
SELECT column_name, data_type, column_default
FROM information_schema.columns
WHERE table_name = 'products' AND column_name = 'sort_order';
