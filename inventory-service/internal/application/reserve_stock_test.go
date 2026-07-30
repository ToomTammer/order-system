package application_test

import (
	"context"
	"testing"

	"inventory-service/internal/application"
	"inventory-service/internal/domain"
)

type fakeStore struct {
	processed        map[string]bool
	available        map[string]int
	unknownProducts  map[string]bool
	reserveCallCount int
}

func newFakeStore() *fakeStore {
	return &fakeStore{
		processed: map[string]bool{},
		available: map[string]int{"sku-1": 5},
	}
}

func (s *fakeStore) ReserveIfNotProcessed(ctx context.Context, messageID, productID string, quantity int) (bool, domain.ReservationResult, error) {
	if s.processed[messageID] {
		return true, domain.ReservationResult{}, nil
	}
	s.processed[messageID] = true
	s.reserveCallCount++

	available, known := s.available[productID]
	if !known {
		return false, domain.ReservationResult{Reserved: false, Reason: "unknown product"}, nil
	}
	if available < quantity {
		return false, domain.ReservationResult{Reserved: false, Reason: "insufficient stock"}, nil
	}
	s.available[productID] = available - quantity
	return false, domain.ReservationResult{Reserved: true}, nil
}

type fakePublisher struct {
	reservedCalls []string
	failedCalls   []string
}

func (p *fakePublisher) PublishStockReserved(ctx context.Context, orderID, productID, correlationID string) error {
	p.reservedCalls = append(p.reservedCalls, orderID)
	return nil
}

func (p *fakePublisher) PublishStockFailed(ctx context.Context, orderID, productID, reason, correlationID string) error {
	p.failedCalls = append(p.failedCalls, orderID)
	return nil
}

func TestHandle_PublishesStockReserved_WhenStockAvailable(t *testing.T) {
	store := newFakeStore()
	publisher := &fakePublisher{}
	uc := application.NewReserveStockUseCase(store, publisher)

	err := uc.Handle(context.Background(), "msg-1", "corr-1", application.OrderCreatedEvent{
		OrderID: "order-1", ProductID: "sku-1", Quantity: 2,
	})

	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(publisher.reservedCalls) != 1 || len(publisher.failedCalls) != 0 {
		t.Fatalf("expected exactly one StockReserved publish, got reserved=%v failed=%v", publisher.reservedCalls, publisher.failedCalls)
	}
}

func TestHandle_PublishesStockFailed_WhenInsufficientStock(t *testing.T) {
	store := newFakeStore()
	publisher := &fakePublisher{}
	uc := application.NewReserveStockUseCase(store, publisher)

	err := uc.Handle(context.Background(), "msg-1", "corr-1", application.OrderCreatedEvent{
		OrderID: "order-1", ProductID: "sku-1", Quantity: 100,
	})

	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(publisher.failedCalls) != 1 || len(publisher.reservedCalls) != 0 {
		t.Fatalf("expected exactly one StockFailed publish, got reserved=%v failed=%v", publisher.reservedCalls, publisher.failedCalls)
	}
}

func TestHandle_IsIdempotent_DuplicateMessageIDNeverReservesTwiceOrRepublishes(t *testing.T) {
	store := newFakeStore()
	publisher := &fakePublisher{}
	uc := application.NewReserveStockUseCase(store, publisher)
	event := application.OrderCreatedEvent{OrderID: "order-1", ProductID: "sku-1", Quantity: 2}

	if err := uc.Handle(context.Background(), "msg-1", "corr-1", event); err != nil {
		t.Fatalf("unexpected error on first delivery: %v", err)
	}
	if err := uc.Handle(context.Background(), "msg-1", "corr-1", event); err != nil {
		t.Fatalf("unexpected error on duplicate delivery: %v", err)
	}

	if store.reserveCallCount != 1 {
		t.Fatalf("expected exactly one reservation attempt, got %d", store.reserveCallCount)
	}
	if len(publisher.reservedCalls) != 1 {
		t.Fatalf("expected exactly one StockReserved publish across both deliveries, got %v", publisher.reservedCalls)
	}
	if store.available["sku-1"] != 3 {
		t.Fatalf("expected stock decremented exactly once (5 - 2 = 3), got %d", store.available["sku-1"])
	}
}
