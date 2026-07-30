package domain

import "context"

type ReservationResult struct {
	Reserved bool
	Reason string
}

type ReservationStore interface {
	ReserveIfNotProcessed(ctx context.Context, messageID, productID string, quantity int) (alreadyProcessed bool, result ReservationResult, err error)
}


type EventPublisher interface {
	PublishStockReserved(ctx context.Context, orderID, productID, correlationID string) error
	PublishStockFailed(ctx context.Context, orderID, productID, reason, correlationID string) error
}
