package application

import (
	"context"
	"log/slog"

	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/attribute"

	"inventory-service/internal/domain"
)

type OrderCreatedEvent struct {
	OrderID   string
	ProductID string
	Quantity  int
}

type ReserveStockUseCase struct {
	Store     domain.ReservationStore
	Publisher domain.EventPublisher
}

func NewReserveStockUseCase(store domain.ReservationStore, publisher domain.EventPublisher) *ReserveStockUseCase {
	return &ReserveStockUseCase{Store: store, Publisher: publisher}
}

var tracer = otel.Tracer("inventory-service")


func (uc *ReserveStockUseCase) Handle(ctx context.Context, messageID, correlationID string, event OrderCreatedEvent) error {
	ctx, span := tracer.Start(ctx, "ReserveStock")
	defer span.End()
	span.SetAttributes(
		attribute.String("order.id", event.OrderID),
		attribute.String("correlation.id", correlationID),
		attribute.String("message.id", messageID),
	)

	log := slog.With("orderId", event.OrderID, "correlationId", correlationID, "messageId", messageID)

	alreadyProcessed, result, err := uc.Store.ReserveIfNotProcessed(ctx, messageID, event.ProductID, event.Quantity)
	if err != nil {
		return err
	}
	if alreadyProcessed {
		log.Info("duplicate OrderCreated delivery, already processed, skipping")
		return nil
	}

	if result.Reserved {
		log.Info("stock reserved, publishing StockReserved")
		return uc.Publisher.PublishStockReserved(ctx, event.OrderID, event.ProductID, correlationID)
	}
	log.Info("stock reservation failed, publishing StockFailed", "reason", result.Reason)
	return uc.Publisher.PublishStockFailed(ctx, event.OrderID, event.ProductID, result.Reason, correlationID)
}
