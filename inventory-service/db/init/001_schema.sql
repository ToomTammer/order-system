CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE stock (
    product_id text PRIMARY KEY,
    available  integer NOT NULL CHECK (available >= 0),
    reserved   integer NOT NULL DEFAULT 0 CHECK (reserved >= 0)
);

CREATE TABLE processed_events (
    message_id   text PRIMARY KEY,
    processed_at timestamptz NOT NULL DEFAULT now()
);

INSERT INTO stock (product_id, available) VALUES
    ('sku-123', 100),
    ('sku-1', 50),
    ('sku-2', 50),
    ('sku-crash-test', 10),
    ('sku-low-stock', 2);
