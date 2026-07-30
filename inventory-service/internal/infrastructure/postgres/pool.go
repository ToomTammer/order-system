package postgres

import (
	"context"
	"fmt"
	"log/slog"
	"time"

	"github.com/jackc/pgx/v5/pgxpool"
)

func Connect(ctx context.Context, connString string, maxAttempts int, delay time.Duration) (*pgxpool.Pool, error) {
	var lastErr error
	for attempt := 1; attempt <= maxAttempts; attempt++ {
		pool, err := pgxpool.New(ctx, connString)
		if err == nil {
			if pingErr := pool.Ping(ctx); pingErr == nil {
				return pool, nil
			} else {
				lastErr = pingErr
				pool.Close()
			}
		} else {
			lastErr = err
		}

		slog.Warn("postgres connect attempt failed, retrying", "attempt", attempt, "maxAttempts", maxAttempts, "error", lastErr)
		time.Sleep(delay)
	}

	return nil, fmt.Errorf("could not connect to postgres after %d attempts: %w", maxAttempts, lastErr)
}
