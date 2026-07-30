package rabbitmq

import (
	"context"
	"encoding/json"
	"log/slog"

	"github.com/google/uuid"
	amqp "github.com/rabbitmq/amqp091-go"

	"inventory-service/internal/application"
)

const orderCreatedQueue = "inventory-service.order-created"

type orderCreatedPayload struct {
	OrderID   string `json:"orderId"`
	ProductID string `json:"productId"`
	Quantity  int    `json:"quantity"`
}

func ConsumeOrderCreated(conn *amqp.Connection, useCase *application.ReserveStockUseCase) error {
	ch, err := conn.Channel()
	if err != nil {
		return err
	}

	if err := DeclareWithRetry(ch, orderCreatedQueue, OrdersExchange, "OrderCreated"); err != nil {
		return err
	}
	if err := ch.Qos(10, 0, false); err != nil {
		return err
	}

	deliveries, err := ch.Consume(orderCreatedQueue, "", false, false, false, false, nil)
	if err != nil {
		return err
	}

	slog.Info("consuming order-created queue", "queue", orderCreatedQueue)
	for delivery := range deliveries {
		handleDelivery(ch, delivery, useCase)
	}
	return nil
}

func handleDelivery(ch *amqp.Channel, delivery amqp.Delivery, useCase *application.ReserveStockUseCase) {
	messageID := headerString(delivery.Headers, "x-message-id")
	if messageID == "" {
		messageID = uuid.NewString()
	}
	correlationID := headerString(delivery.Headers, "x-correlation-id")
	if correlationID == "" {
		correlationID = uuid.NewString()
	}

	var payload orderCreatedPayload
	if err := json.Unmarshal(delivery.Body, &payload); err != nil {
		slog.Error("failed to parse OrderCreated payload, dropping to DLQ", "error", err, "correlationId", correlationID)
		giveUp(ch, delivery)
		return
	}

	event := application.OrderCreatedEvent{
		OrderID:   payload.OrderID,
		ProductID: payload.ProductID,
		Quantity:  payload.Quantity,
	}

	if err := useCase.Handle(context.Background(), messageID, correlationID, event); err != nil {
		attempts := DeathCount(delivery.Headers, orderCreatedQueue)
		if attempts+1 >= MaxAttempts {
			slog.Error("giving up on OrderCreated message after max attempts, sending to DLQ", "error", err, "attempts", attempts+1, "orderId", payload.OrderID, "correlationId", correlationID)
			giveUp(ch, delivery)
			return
		}
		slog.Warn("failed to handle OrderCreated, will retry via queue.retry", "error", err, "attempts", attempts+1, "orderId", payload.OrderID, "correlationId", correlationID)
		_ = delivery.Nack(false, false)
		return
	}

	_ = delivery.Ack(false)
}


func giveUp(ch *amqp.Channel, delivery amqp.Delivery) {
	err := SendToDeadLetterQueue(context.Background(), ch, orderCreatedQueue, amqp.Publishing{
		ContentType:  delivery.ContentType,
		DeliveryMode: amqp.Persistent,
		Headers:      delivery.Headers,
		Body:         delivery.Body,
	})
	if err != nil {
		slog.Error("failed to publish to DLQ, nacking with requeue as last resort", "error", err)
		_ = delivery.Nack(false, true)
		return
	}
	_ = delivery.Ack(false)
}

func headerString(headers amqp.Table, key string) string {
	if v, ok := headers[key]; ok {
		if s, ok := v.(string); ok {
			return s
		}
	}
	return ""
}
