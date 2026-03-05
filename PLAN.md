# Troop Leader Feature - Implementation Plan

## Overview

Expand the app from a single-family cookie tracker into a connected troop management platform with two account types: **Family Account** (current functionality, scoped per-family) and **Troop Leader Account** (troop-wide management). When both share a troop number, family activity auto-syncs to the troop leader's view.

---

## Phase 1: Multi-Tenancy Foundation (Account Scoping)

**Goal:** Every piece of data belongs to an account. Current single-user behavior is preserved but scoped.

### 1A. New Database Entities

**`accounts` table:**
```sql
CREATE TABLE accounts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    supabase_user_id TEXT NOT NULL UNIQUE,  -- links to Supabase Auth user
    account_type TEXT NOT NULL DEFAULT 'family',  -- 'family' or 'troop_leader'
    troop_number TEXT,  -- shared identifier for linking
    display_name TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);
CREATE INDEX idx_accounts_supabase_user ON accounts(supabase_user_id);
CREATE INDEX idx_accounts_troop_number ON accounts(troop_number);
```

**Add `account_id` FK to all existing tables:**
- girl_scouts, customers, orders, products, inventory_batches, inventory_returns, booth_sessions, paybacks

### 1B. Account Entity & DbContext Changes

- Create `Data/Account.cs` entity
- Add `AccountId` FK + navigation property to all existing entities
- Add **global query filter** in `AppDbContext.OnModelCreating()`:
  ```csharp
  modelBuilder.Entity<Order>().HasQueryFilter(o => o.AccountId == _currentAccountId);
  // repeat for all tenant-scoped entities
  ```
- Inject `ICurrentAccountService` into `AppDbContext` to get the current account ID

### 1C. Current Account Service & Middleware

- Create `Services/ICurrentAccountService.cs` interface with `Guid? AccountId` property
- Create `Services/CurrentAccountService.cs` (scoped) that reads AccountId from `HttpContext.User.Claims`
- Add middleware in `Program.cs` to resolve account from claims on each request

### 1D. Update Authentication Flow

- Extend `SignInCookie()` to include `AccountId` and `AccountType` claims
- On login: look up `accounts` table by `supabase_user_id`, add claims
- First-time login: redirect to account setup page (Phase 2)

### 1E. Migration: Existing Data

- Create a default account for existing data
- Run SQL to set `account_id` on all existing rows
- Add `NOT NULL` constraint after backfill

**Files changed:** `Data/Account.cs` (new), `Data/AppDbContext.cs`, all 12 entity files, `Services/ICurrentAccountService.cs` (new), `Services/CurrentAccountService.cs` (new), `Controllers/AccountController.cs`, `Program.cs`, `Migrations/add_multi_tenancy.sql` (new)

---

## Phase 2: Account Setup & Registration

**Goal:** New users choose account type and set up their account.

### 2A. Account Setup Page

- After first Supabase login, if no `accounts` row exists, redirect to `/Account/Setup`
- User chooses: **Family Account** or **Troop Leader Account**
- Family: enter display name, troop number (optional)
- Troop Leader: enter display name, troop number (required)

### 2B. Auto-Create Default Girl Scout (Family)

- Family accounts get a default Girl Scout record created
- Troop Leader accounts skip this

**Files changed:** `Views/Account/Setup.cshtml` (new), `Models/AccountSetupViewModel.cs` (new), `Controllers/AccountController.cs`

---

## Phase 3: Troop Leader - Core Features

**Goal:** Troop Leader gets a separate dashboard and management tools.

### 3A. Troop-Level Entities

**`troop_consignment` table** (cookies received from council):
```sql
CREATE TABLE troop_consignment (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES accounts(id),
    product_id UUID NOT NULL REFERENCES products(id),
    quantity_boxes INT NOT NULL,
    received_at TIMESTAMPTZ DEFAULT now(),
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT now()
);
```

**`troop_distribution` table** (cookies given to scouts):
```sql
CREATE TABLE troop_distribution (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES accounts(id),
    girl_scout_id UUID NOT NULL REFERENCES girl_scouts(id),
    product_id UUID NOT NULL REFERENCES products(id),
    quantity_boxes INT NOT NULL,
    distributed_at TIMESTAMPTZ DEFAULT now(),
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT now()
);
```

**`troop_scout_payback` table** (scout payments back to troop):
```sql
CREATE TABLE troop_scout_payback (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES accounts(id),
    girl_scout_id UUID NOT NULL REFERENCES girl_scouts(id),
    amount NUMERIC(10,2) NOT NULL,
    paid_at TIMESTAMPTZ DEFAULT now(),
    method TEXT,
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT now()
);
```

### 3B. Troop Leader Controllers

- **`TroopController.cs`** - Dashboard with troop-level KPIs
- **`TroopInventoryController.cs`** - Consignment receipt, distribution to scouts
- **`TroopPaybackController.cs`** - Per-scout payback tracking, troop-to-council payback
- **`TroopReportsController.cs`** - Troop-level and per-scout reports

### 3C. Troop Leader Dashboard

KPIs:
- Total boxes received from consignment
- Total boxes distributed to scouts
- Total boxes sold (across all scouts)
- Total amount collected / outstanding
- Per-scout summary cards

### 3D. Troop Leader Views

- Troop Inventory (consignment in, distribution out, current stock)
- Scout Management (list scouts, per-scout detail pages)
- Per-Scout View: boxes received, boxes sold, amount owed, payback history
- Booth Sessions (troop-wide view)

**Files changed:** 4 new controllers, ~12 new views, 3 new entity files, new view models, `Migrations/add_troop_entities.sql` (new)

---

## Phase 4: Connected Data (Family <-> Troop Linking)

**Goal:** When a Family account and a Troop Leader account share a troop number, the family's activity is visible to the troop leader.

### 4A. Linking Logic

- Troop Leader queries for all Family accounts with matching `troop_number`
- Read-only access to linked family data (orders, booth sales, inventory)
- No write access to family data from troop side

### 4B. Implementation

- `TroopController` queries across accounts where `troop_number` matches
- Use `.IgnoreQueryFilters()` selectively for cross-account reads
- Add authorization checks to ensure only troop leaders can cross-query
- Family dashboard shows "Connected to Troop [number]" indicator

### 4C. Sync Points

What the Troop Leader sees from linked families:
- Orders placed (aggregated per scout)
- Booth session participation and sales
- Payback/collection status
- Inventory received by each family

What the Family sees:
- Their own data (unchanged)
- "Part of Troop [number]" badge if linked
- Optionally: troop-wide stats (total sold, rank)

**Files changed:** `TroopController.cs` (updated), new views for linked data, `Services/TroopLinkingService.cs` (new)

---

## Phase 5: Navigation & Role-Based UI

**Goal:** Different navigation menus and layouts for Family vs Troop Leader.

### 5A. Layout Changes

- Shared `_Layout.cshtml` with conditional nav based on `AccountType` claim
- Family: current navigation (Home, Orders, Inventory, Booth, Reports, etc.)
- Troop Leader: Troop Dashboard, Consignment, Distribution, Scouts, Booth, Reports, Settings

### 5B. Authorization

- `[Authorize(Roles = "troop_leader")]` on troop-specific controllers
- `[Authorize(Roles = "family")]` on family-specific controllers
- Shared controllers (Products, Account) accessible to both

**Files changed:** `Views/Shared/_Layout.cshtml`, `Views/Shared/_TroopLayout.cshtml` (new), authorization attributes on controllers

---

## Phase 6: Troop-Level Reporting

**Goal:** Comprehensive reports for troop leaders.

Reports:
1. **Troop Inventory Report** - Consignment received, distributed, remaining
2. **Per-Scout Sales Report** - Each scout's orders, booth sales, totals
3. **Troop Payback Report** - Amount each scout owes, paid, outstanding
4. **Troop Booth Summary** - All booth sessions, totals, per-scout breakdown
5. **Council Return Report** - Boxes to return, amount owed to council

**Files changed:** `TroopReportsController.cs`, ~5 new report views, new view models

---

## Implementation Order & Estimates

| Phase | Description | Scope |
|-------|-------------|-------|
| **Phase 1** | Multi-tenancy foundation | ~20 files, 1 migration |
| **Phase 2** | Account setup flow | ~5 files |
| **Phase 3** | Troop leader core features | ~20 new files, 1 migration |
| **Phase 4** | Family-Troop linking | ~5 files |
| **Phase 5** | Navigation & role-based UI | ~5 files |
| **Phase 6** | Troop reporting | ~10 files |

**Phase 1 is the critical foundation** - everything else builds on it. It's also the highest-risk phase since it touches every existing entity and controller.

---

## Key Architecture Decisions

1. **Account-based scoping** (not Supabase RLS) - Keeps logic in the application layer where it's testable and debuggable
2. **Global query filters** - Automatic tenant isolation without modifying every query
3. **Shared Products table** - Products are global (same cookie varieties for everyone), not per-account
4. **Read-only cross-account linking** - Troop leaders can view but not modify family data
5. **SQL migrations** - Continue the existing pattern of manual SQL files run in Supabase SQL Editor
6. **Claims-based account context** - AccountId and AccountType stored in auth cookie, read by middleware

---

## Shall I proceed?

I recommend starting with **Phase 1** (multi-tenancy foundation) as it's the prerequisite for everything else. This involves:
- Creating the Account entity and migration SQL
- Adding AccountId to all entities
- Implementing the current account service
- Updating the auth flow
- Adding global query filters
- Backfilling existing data

Phases 2-6 can then be built incrementally on top of the foundation.
