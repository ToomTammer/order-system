package postgres

import (
	"context"

	"github.com/jackc/pgx/v5/pgxpool"

	"inventory-service/internal/domain"
)

type ReservationStore struct {
	pool *pgxpool.Pool
}

func NewReservationStore(pool *pgxpool.Pool) *ReservationStore {
	return &ReservationStore{pool: pool}
}

func (s *ReservationStore) ReserveIfNotProcessed(ctx context.Context, messageID, productID string, quantity int) (bool, domain.ReservationResult, error) {
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return false, domain.ReservationResult{}, err
	}
	defer tx.Rollback(ctx) // no-op once Commit has succeeded

	idempotencyTag, err := tx.Exec(ctx, `INSERT INTO processed_events (message_id) VALUES ($1) ON CONFLICT DO NOTHING`, messageID)
	if err != nil {
		return false, domain.ReservationResult{}, err
	}
	if idempotencyTag.RowsAffected() == 0 {
		if err := tx.Commit(ctx); err != nil {
			return false, domain.ReservationResult{}, err
		}
		return true, domain.ReservationResult{}, nil
	}

	updateTag, err := tx.Exec(ctx,
		`UPDATE stock SET available = available - $1, reserved = reserved + $1 WHERE product_id = $2 AND available >= $1`,
		quantity, productID)
	if err != nil {
		return false, domain.ReservationResult{}, err
	}

	var result domain.ReservationResult
	if updateTag.RowsAffected() == 0 {
		var exists bool
		if err := tx.QueryRow(ctx, `SELECT EXISTS(SELECT 1 FROM stock WHERE product_id = $1)`, productID).Scan(&exists); err != nil {
			return false, domain.ReservationResult{}, err
		}
		reason := "insufficient stock"
		if !exists {
			reason = "unknown product"
		}
		result = domain.ReservationResult{Reserved: false, Reason: reason}
	} else {
		result = domain.ReservationResult{Reserved: true}
	}

	if err := tx.Commit(ctx); err != nil {
		return false, domain.ReservationResult{}, err
	}
	return false, result, nil
}
