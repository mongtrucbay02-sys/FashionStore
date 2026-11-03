-- FashionStore schema for Supabase PostgreSQL.
-- Run this in Supabase Dashboard > SQL Editor as the project database owner.
-- Safe to re-run; this creates only the app's private JSONB storage tables.

CREATE SCHEMA IF NOT EXISTS app_private;

CREATE TABLE IF NOT EXISTS app_private.store_collections
(
    collection_name text PRIMARY KEY
        CHECK (collection_name IN (
            'categories',
            'products',
            'users',
            'orders',
            'reviews',
            'stock-movements',
            'outfit-products',
            'qr-payment-settings',
            'inventory-before-sizes'
        )),
    data jsonb NOT NULL DEFAULT '[]'::jsonb
        CHECK (jsonb_typeof(data) = 'array'),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS app_private.import_history
(
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    imported_at timestamptz NOT NULL DEFAULT now(),
    source_description text NOT NULL
);

COMMENT ON SCHEMA app_private IS
    'Private application storage. Store data is accessed by the ASP.NET backend through PostgreSQL, not the Supabase Data API.';

COMMENT ON TABLE app_private.store_collections IS
    'FashionStore collections serialized as JSONB arrays for the existing AppStore backend.';
