package rabbitmq

import (
	"fmt"
	"log/slog"
	"time"

	amqp "github.com/rabbitmq/amqp091-go"
)

const (
	OrdersExchange      = "orders"
	DeadLetterExchange  = "orders.dlx"
)

func Connect(host string, port int, user, password string, maxAttempts int, delay time.Duration) (*amqp.Connection, error) {
	url := fmt.Sprintf("amqp://%s:%s@%s:%d/", user, password, host, port)

	var lastErr error
	for attempt := 1; attempt <= maxAttempts; attempt++ {
		conn, err := amqp.Dial(url)
		if err == nil {
			ch, err := conn.Channel()
			if err == nil {
				if declareErr := declareTopology(ch); declareErr == nil {
					ch.Close()
					return conn, nil
				} else {
					lastErr = declareErr
				}
			} else {
				lastErr = err
			}
			conn.Close()
		} else {
			lastErr = err
		}

		slog.Warn("rabbitmq connect attempt failed, retrying", "attempt", attempt, "maxAttempts", maxAttempts, "error", lastErr)
		time.Sleep(delay)
	}

	return nil, fmt.Errorf("could not connect to rabbitmq at %s:%d after %d attempts: %w", host, port, maxAttempts, lastErr)
}

func declareTopology(ch *amqp.Channel) error {
	if err := ch.ExchangeDeclare(OrdersExchange, "topic", true, false, false, false, nil); err != nil {
		return err
	}
	if err := ch.ExchangeDeclare(DeadLetterExchange, "fanout", true, false, false, false, nil); err != nil {
		return err
	}
	return nil
}
