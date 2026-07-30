package integration

import (
	"context"
	"fmt"
	"path/filepath"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/testcontainers/testcontainers-go"
	postgrestc "github.com/testcontainers/testcontainers-go/modules/postgres"
	"github.com/testcontainers/testcontainers-go/wait"

	pginfra "inventory-service/internal/infrastructure/postgres"
)

func startPostgres(t *testing.T) *pgxpool.Pool {
	t.Helper()
	ctx := context.Background()

	schemaPath, err := filepath.Abs("../db/init/001_schema.sql")
	if err != nil {
		t.Fatalf("resolve schema path: %v", err)
	}

	container, err := postgrestc.Run(ctx, "postgres:16-alpine",
		postgrestc.WithDatabase("inventory_db"),
		postgrestc.WithUsername("postgres"),
		postgrestc.WithPassword("postgres"),
		postgrestc.WithInitScripts(schemaPath),
		testcontainers.WithWaitStrategy(wait.ForLog("database system is ready to accept connections").WithOccurrence(2).WithStartupTimeout(60*time.Second)),
	)
	if err != nil {
		t.Fatalf("start postgres container: %v", err)
	}
	t.Cleanup(func() { _ = container.Terminate(context.Background()) })

	connString, err := container.ConnectionString(ctx, "sslmode=disable")
	if err != nil {
		t.Fatalf("get connection string: %v", err)
	}

	pool, err := pgxpool.New(ctx, connString)
	if err != nil {
		t.Fatalf("connect pool: %v", err)
	}
	t.Cleanup(pool.Close)

	return pool
}

func TestReserveIfNotProcessed_ReservesAtomically(t *testing.T) {
	pool := startPostgres(t)
	store := pginfra.NewReservationStore(pool)
	ctx := context.Background()

	alreadyProcessed, result, err := store.ReserveIfNotProcessed(ctx, "msg-1", "sku-123", 5)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if alreadyProcessed {
		t.Fatal("expected first delivery to not be marked as already processed")
	}
	if !result.Reserved {
		t.Fatalf("expected reservation to succeed, got reason: %s", result.Reason)
	}
}

func TestReserveIfNotProcessed_InsufficientStock(t *testing.T) {
	pool := startPostgres(t)
	store := pginfra.NewReservationStore(pool)
	ctx := context.Background()

	_, result, err := store.ReserveIfNotProcessed(ctx, "msg-1", "sku-low-stock", 1000)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if result.Reserved {
		t.Fatal("expected reservation to fail for insufficient stock")
	}
	if result.Reason != "insufficient stock" {
		t.Fatalf("expected reason 'insufficient stock', got %q", result.Reason)
	}
}

func TestReserveIfNotProcessed_IsIdempotent_DuplicateMessageIDNeverReservesTwice(t *testing.T) {
	pool := startPostgres(t)
	store := pginfra.NewReservationStore(pool)
	ctx := context.Background()

	messageID := "duplicate-msg"
	_, first, err := store.ReserveIfNotProcessed(ctx, messageID, "sku-123", 3)
	if err != nil || !first.Reserved {
		t.Fatalf("first delivery should reserve successfully: reserved=%v err=%v", first.Reserved, err)
	}

	alreadyProcessed, _, err := store.ReserveIfNotProcessed(ctx, messageID, "sku-123", 3)
	if err != nil {
		t.Fatalf("unexpected error on duplicate delivery: %v", err)
	}
	if !alreadyProcessed {
		t.Fatal("expected duplicate messageID to be reported as already processed")
	}

	var available int
	if err := pool.QueryRow(ctx, "SELECT available FROM stock WHERE product_id = $1", "sku-123").Scan(&available); err != nil {
		t.Fatalf("query stock: %v", err)
	}
	if available != 97 { // seeded 100 - 3, only once
		t.Fatalf("expected stock decremented exactly once (100-3=97), got %d", available)
	}
}

func TestReserveIfNotProcessed_ConcurrentReservations_NeverOversell(t *testing.T) {
	pool := startPostgres(t)
	store := pginfra.NewReservationStore(pool)
	ctx := context.Background()

	const attempts = 10
	var succeeded atomic.Int32
	var wg sync.WaitGroup
	wg.Add(attempts)
	for i := 0; i < attempts; i++ {
		go func(i int) {
			defer wg.Done()
			_, result, err := store.ReserveIfNotProcessed(ctx, fmt.Sprintf("concurrent-msg-%d", i), "sku-low-stock", 1)
			if err != nil {
				t.Errorf("unexpected error: %v", err)
				return
			}
			if result.Reserved {
				succeeded.Add(1)
			}
		}(i)
	}
	wg.Wait()

	if got := succeeded.Load(); got != 2 {
		t.Fatalf("expected exactly 2 successful reservations (seeded available=2), got %d", got)
	}

	var available int
	if err := pool.QueryRow(ctx, "SELECT available FROM stock WHERE product_id = $1", "sku-low-stock").Scan(&available); err != nil {
		t.Fatalf("query stock: %v", err)
	}
	if available != 0 {
		t.Fatalf("expected available=0 after exhausting the 2-unit SKU, got %d", available)
	}
}
