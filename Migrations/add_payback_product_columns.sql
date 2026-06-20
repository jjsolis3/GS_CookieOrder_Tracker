-- Migration: Add product_id and quantity_boxes columns to paybacks table
-- Run this in Supabase SQL Editor

-- Add product_id column (nullable FK to products)
ALTER TABLE paybacks
ADD COLUMN IF NOT EXISTS product_id uuid REFERENCES products(id);

-- Add quantity_boxes column (for product-based paybacks without a matching order)
ALTER TABLE paybacks
ADD COLUMN IF NOT EXISTS quantity_boxes integer;

-- Verify the columns were added
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'paybacks' AND column_name IN ('product_id', 'quantity_boxes');
