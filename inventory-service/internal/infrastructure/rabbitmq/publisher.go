package rabbitmq

import (
	"context"
	"encoding/json"

	"github.com/google/uuid"
	amqp "github.com/rabbitmq/amqp091-go"
)

type stockReservedPayload struct {
	OrderID   string `json:"orderId"`
	ProductID string `json:"productId"`
}

type stockFailedPayload struct {
	OrderID   string `json:"orderId"`
	ProductID string `json:"productId"`
	Reason    string `json:"reason"`
}

// Publisher implements domain.EventPublisher over a dedicated channel on the
// shared connection.
type Publisher struct {
	channel *amqp.Channel
}

func NewPublisher(conn *amqp.Connection) (*Publisher, error) {
	ch, err := conn.Channel()
	if err != nil {
		return nil, err
	}
	return &Publisher{channel: ch}, nil
}

func (p *Publisher) PublishStockReserved(ctx context.Context, orderID, productID, correlationID string) error {
	body, err := json.Marshal(stockReservedPayload{OrderID: orderID, ProductID: productID})
	if err != nil {
		return err
	}
	return p.publish(ctx, "StockReserved", correlationID, body)
}

func (p *Publisher) PublishStockFailed(ctx context.Context, orderID, productID, reason, correlationID string) error {
	body, err := json.Marshal(stockFailedPayload{OrderID: orderID, ProductID: productID, Reason: reason})
	if err != nil {
		return err
	}
	return p.publish(ctx, "StockFailed", correlationID, body)
}

func (p *Publisher) publish(ctx context.Context, routingKey, correlationID string, body []byte) error {
	return p.channel.PublishWithContext(ctx, OrdersExchange, routingKey, false, false, amqp.Publishing{
		ContentType:  "application/json",
		DeliveryMode: amqp.Persistent,
		Body:         body,
		Headers: amqp.Table{
			"x-message-id":     uuid.NewString(),
			"x-correlation-id": correlationID,
		},
	})
}
