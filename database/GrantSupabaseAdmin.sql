-- Promote an existing, verified, unlocked account in the Supabase-backed app store.
-- Run in Supabase Dashboard > SQL Editor as the database owner.
-- The ASP.NET app currently stores users as PascalCase JSON objects in the
-- app_private.store_collections JSONB array (collection_name = 'users').

CREATE SCHEMA IF NOT EXISTS app_private;

CREATE TABLE IF NOT EXISTS app_private.store_collections
(
    collection_name text PRIMARY KEY
        CHECK (collection_name IN (
            'categories', 'products', 'users', 'orders', 'reviews',
            'stock-movements', 'outfit-products', 'qr-payment-settings',
            'inventory-before-sizes'
        )),
    data jsonb NOT NULL DEFAULT '[]'::jsonb
        CHECK (jsonb_typeof(data) = 'array'),
    updated_at timestamptz NOT NULL DEFAULT now()
);

DO $$
DECLARE
    affected_rows integer;
BEGIN
    UPDATE app_private.store_collections AS collections
    SET data = (
        SELECT COALESCE(
            jsonb_agg(
                CASE
                    WHEN lower(user_row->>'Email') = lower('mongtrucbay02@gmail.com')
                    THEN jsonb_set(user_row, '{Role}', '"Admin"'::jsonb, true)
                    ELSE user_row
                END
                ORDER BY ordinal
            ),
            '[]'::jsonb
        )
        FROM jsonb_array_elements(collections.data) WITH ORDINALITY AS users(user_row, ordinal)
    )
    WHERE collections.collection_name = 'users'
      AND EXISTS (
          SELECT 1
          FROM jsonb_array_elements(collections.data) AS users(user_row)
          WHERE lower(user_row->>'Email') = lower('mongtrucbay02@gmail.com')
            AND user_row->>'EmailVerifiedAt' IS NOT NULL
            AND COALESCE((user_row->>'IsLocked')::boolean, false) = false
      );

    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    IF affected_rows <> 1 THEN
        RAISE EXCEPTION 'No change made: account must exist in Supabase, have a verified email, and be unlocked.';
    END IF;
END $$;

-- Show only the account identity and access state; do not expose the password hash.
SELECT
    user_row->>'Email' AS email,
    user_row->>'Role' AS role,
    (user_row->>'EmailVerifiedAt') IS NOT NULL AS email_verified,
    COALESCE((user_row->>'IsLocked')::boolean, false) AS is_locked
FROM app_private.store_collections AS collections
CROSS JOIN LATERAL jsonb_array_elements(collections.data) AS users(user_row)
WHERE collections.collection_name = 'users'
  AND lower(user_row->>'Email') = lower('mongtrucbay02@gmail.com');
