#!/bin/bash
# Runs once, on first container start (docker-entrypoint-initdb.d convention).

set -euo pipefail

for db in orders_db inventory_db; do
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
    CREATE DATABASE ${db};
EOSQL
done

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" -d orders_db -f /sql/orders/001_schema.sql
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" -d inventory_db -f /sql/inventory/001_schema.sql