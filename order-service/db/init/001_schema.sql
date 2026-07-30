-- step 1: users + orders, ownership (user_id) from day one.
CREATE TABLE users (
    id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    username               text NOT NULL UNIQUE,
    password_hash          text NOT NULL,
    failed_login_attempts  integer NOT NULL DEFAULT 0,
    locked_until           timestamptz,
    created_at             timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE orders (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    product_id  text NOT NULL,
    quantity    integer NOT NULL CHECK (quantity > 0),
    status      text NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending', 'Confirmed', 'Failed')),
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX idx_orders_user_id ON orders(user_id);

-- step 2: transactional outbox (order events dispatched to RabbitMQ by OutboxDispatcherService).
CREATE TABLE outbox_messages (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    aggregate_id    uuid NOT NULL,
    event_type      text NOT NULL,
    payload         jsonb NOT NULL,
    correlation_id  uuid NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    processed_at    timestamptz,
    attempts        integer NOT NULL DEFAULT 0
);
CREATE INDEX idx_outbox_messages_processed_at ON outbox_messages(processed_at);

-- step 3: inbox for idempotent consumption of stock-result events (OrderEventsConsumer).
CREATE TABLE processed_inbox_messages (
    message_id   uuid PRIMARY KEY,
    processed_at timestamptz NOT NULL DEFAULT now()
);

-- step 4: refresh tokens for the auth workflow.
CREATE TABLE refresh_tokens (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    token_hash  text NOT NULL UNIQUE,
    expires_at  timestamptz NOT NULL,
    revoked_at  timestamptz,
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX idx_refresh_tokens_user_id ON refresh_tokens(user_id);