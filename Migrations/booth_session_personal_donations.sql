-- Migration: Add personal inventory toggle and donation tracking to booth_sessions
-- Date: 2026-03-04
-- Description: Adds use_personal_inventory boolean and total_donations numeric columns
--              to support personal inventory booth sessions and donation tracking.

-- Add personal inventory toggle (default false = troop inventory)
ALTER TABLE booth_sessions ADD COLUMN IF NOT EXISTS use_personal_inventory boolean DEFAULT false;

-- Add donation tracking (accumulated donation amount per session)
ALTER TABLE booth_sessions ADD COLUMN IF NOT EXISTS total_donations numeric(10,2) DEFAULT 0;

-- Add from_personal_inventory flag to booth_sales for per-sale inventory source tracking
ALTER TABLE booth_sales ADD COLUMN IF NOT EXISTS from_personal_inventory boolean DEFAULT false;
