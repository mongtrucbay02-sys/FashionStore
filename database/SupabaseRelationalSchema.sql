-- FashionStore relational schema for Supabase PostgreSQL.
-- Run in Supabase Dashboard > SQL Editor as the project database owner.
-- This is the PostgreSQL counterpart of the table/view definitions in Schema.sql.
-- IDs remain application-assigned, matching the SQL Server schema.

CREATE SCHEMA IF NOT EXISTS fashion_store;

CREATE TABLE IF NOT EXISTS fashion_store.schema_versions (
    version integer PRIMARY KEY,
    applied_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS fashion_store.categories (
    id integer PRIMARY KEY,
    name varchar(200) NOT NULL,
    description text NOT NULL
);

CREATE TABLE IF NOT EXISTS fashion_store.users (
    id uuid PRIMARY KEY,
    full_name varchar(200) NOT NULL,
    email varchar(254) NOT NULL UNIQUE,
    password_hash text NOT NULL,
    phone varchar(64) NOT NULL,
    address varchar(2000) NOT NULL,
    role varchar(10) NOT NULL CHECK (role IN ('User', 'Admin')),
    is_locked boolean NOT NULL DEFAULT false,
    email_verified_at timestamptz
);

CREATE TABLE IF NOT EXISTS fashion_store.products (
    id integer PRIMARY KEY,
    name varchar(200) NOT NULL,
    description text NOT NULL,
    price numeric(18,2) NOT NULL CHECK (price >= 0),
    category_id integer NOT NULL REFERENCES fashion_store.categories(id),
    image varchar(1000) NOT NULL,
    color varchar(100) NOT NULL,
    active boolean NOT NULL,
    audience varchar(20) NOT NULL,
    low_stock_threshold integer CHECK (low_stock_threshold IS NULL OR low_stock_threshold BETWEEN 0 AND 1000)
);

CREATE TABLE IF NOT EXISTS fashion_store.orders (
    id integer PRIMARY KEY,
    user_email varchar(254) NOT NULL,
    full_name varchar(200) NOT NULL,
    phone varchar(64) NOT NULL,
    address varchar(2000) NOT NULL,
    payment_method varchar(40) NOT NULL,
    subtotal numeric(18,2) NOT NULL,
    shipping_fee numeric(18,2) NOT NULL,
    total numeric(18,2) NOT NULL,
    stock_deducted boolean NOT NULL,
    status varchar(50) NOT NULL,
    created_at timestamp without time zone NOT NULL,
    CONSTRAINT ck_orders_money CHECK (
        subtotal >= 0 AND shipping_fee >= 0 AND total = subtotal + shipping_fee
    )
);

CREATE TABLE IF NOT EXISTS fashion_store.reviews (
    id integer PRIMARY KEY,
    product_id integer NOT NULL,
    user_key uuid NOT NULL,
    user_id integer NOT NULL,
    user_name varchar(200) NOT NULL,
    rating integer NOT NULL CHECK (rating BETWEEN 1 AND 5),
    content text NOT NULL,
    created_at timestamp without time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS fashion_store.stock_movements (
    id integer PRIMARY KEY,
    product_id integer NOT NULL,
    product_name varchar(200) NOT NULL,
    size varchar(50) NOT NULL,
    type varchar(40) NOT NULL,
    "change" integer NOT NULL,
    stock_after integer NOT NULL,
    note varchar(200) NOT NULL,
    order_id integer,
    "by" varchar(200) NOT NULL,
    created_at timestamp without time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS fashion_store.outfit_products (
    id varchar(80) PRIMARY KEY,
    store_product_id integer NOT NULL REFERENCES fashion_store.products(id),
    category varchar(20) NOT NULL,
    kind varchar(100) NOT NULL,
    name varchar(200) NOT NULL,
    color varchar(100) NOT NULL,
    color_name varchar(100) NOT NULL,
    hex varchar(12) NOT NULL
);

CREATE TABLE IF NOT EXISTS fashion_store.qr_payment_settings (
    id integer PRIMARY KEY CHECK (id = 1),
    enabled boolean NOT NULL,
    bank_bin varchar(6) NOT NULL,
    bank_name varchar(100) NOT NULL,
    account_number varchar(19) NOT NULL,
    account_name varchar(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS fashion_store.product_sizes (
    product_id integer NOT NULL REFERENCES fashion_store.products(id) ON DELETE CASCADE,
    size varchar(50) NOT NULL,
    stock integer NOT NULL CHECK (stock >= 0),
    sort_order integer NOT NULL,
    PRIMARY KEY (product_id, size)
);

CREATE TABLE IF NOT EXISTS fashion_store.order_items (
    order_id integer NOT NULL REFERENCES fashion_store.orders(id) ON DELETE CASCADE,
    line_number integer NOT NULL,
    product_id integer NOT NULL,
    product_name varchar(200) NOT NULL,
    size varchar(50) NOT NULL,
    quantity integer NOT NULL CHECK (quantity > 0),
    unit_price numeric(18,2) NOT NULL CHECK (unit_price >= 0),
    PRIMARY KEY (order_id, line_number)
);

CREATE TABLE IF NOT EXISTS fashion_store.order_qr_payments (
    order_id integer PRIMARY KEY REFERENCES fashion_store.orders(id) ON DELETE CASCADE,
    bank_bin varchar(6) NOT NULL,
    bank_name varchar(100) NOT NULL,
    account_number varchar(19) NOT NULL,
    account_name varchar(100) NOT NULL,
    reference varchar(50) NOT NULL UNIQUE,
    status integer NOT NULL CHECK (status IN (0, 1, 2)),
    reported_at timestamptz,
    paid_at timestamptz,
    confirmed_by varchar(200) NOT NULL,
    bank_transaction_reference varchar(100) NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_qr_bank_transaction
    ON fashion_store.order_qr_payments (bank_transaction_reference)
    WHERE status = 2 AND bank_transaction_reference <> '';

CREATE TABLE IF NOT EXISTS fashion_store.legacy_snapshots (
    name varchar(100) PRIMARY KEY,
    content jsonb NOT NULL
);

CREATE TABLE IF NOT EXISTS fashion_store.import_history (
    id integer PRIMARY KEY CHECK (id = 1),
    imported_at timestamptz NOT NULL,
    source_description varchar(200) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_products_category_id ON fashion_store.products(category_id);
CREATE INDEX IF NOT EXISTS ix_product_sizes_product_id ON fashion_store.product_sizes(product_id);
CREATE INDEX IF NOT EXISTS ix_orders_created_at ON fashion_store.orders(created_at);
CREATE INDEX IF NOT EXISTS ix_reviews_product_id ON fashion_store.reviews(product_id);
CREATE INDEX IF NOT EXISTS ix_stock_movements_product_id ON fashion_store.stock_movements(product_id);

CREATE OR REPLACE VIEW fashion_store.inventory_summary AS
SELECT
    p.id AS product_id,
    p.name,
    p.category_id,
    p.active,
    COALESCE(SUM(ps.stock), 0)::bigint AS total_stock,
    COALESCE(p.low_stock_threshold, 2) AS low_stock_threshold
FROM fashion_store.products AS p
LEFT JOIN fashion_store.product_sizes AS ps ON ps.product_id = p.id
GROUP BY p.id, p.name, p.category_id, p.active, p.low_stock_threshold;

INSERT INTO fashion_store.schema_versions(version)
VALUES (1)
ON CONFLICT (version) DO NOTHING;
