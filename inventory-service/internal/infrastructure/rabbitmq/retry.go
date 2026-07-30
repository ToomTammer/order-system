package rabbitmq

import (
	"context"
	"time"

	amqp "github.com/rabbitmq/amqp091-go"
)

const (
	MaxAttempts = 5
	RetryDelay  = 5 * time.Second
)

func DeclareWithRetry(ch *amqp.Channel, queueName, exchange string, routingKeys ...string) error {
	retryQueue := queueName + ".retry"
	dlq := queueName + ".dlq"

	if _, err := ch.QueueDeclare(queueName, true, false, false, false, amqp.Table{
		"x-dead-letter-exchange":    "",
		"x-dead-letter-routing-key": retryQueue,
	}); err != nil {
		return err
	}
	for _, rk := range routingKeys {
		if err := ch.QueueBind(queueName, rk, exchange, false, nil); err != nil {
			return err
		}
	}

	if _, err := ch.QueueDeclare(retryQueue, true, false, false, false, amqp.Table{
		"x-message-ttl":             int64(RetryDelay / time.Millisecond),
		"x-dead-letter-exchange":    "",
		"x-dead-letter-routing-key": queueName,
	}); err != nil {
		return err
	}

	if _, err := ch.QueueDeclare(dlq, true, false, false, false, nil); err != nil {
		return err
	}

	return nil
}

func DeathCount(headers amqp.Table, queueName string) int64 {
	raw, ok := headers["x-death"]
	if !ok {
		return 0
	}
	deaths, ok := raw.([]interface{})
	if !ok {
		return 0
	}
	for _, d := range deaths {
		death, ok := d.(amqp.Table)
		if !ok {
			continue
		}
		if q, ok := death["queue"].(string); ok && q == queueName {
			switch count := death["count"].(type) {
			case int64:
				return count
			case int32:
				return int64(count)
			}
		}
	}
	return 0
}

func SendToDeadLetterQueue(ctx context.Context, ch *amqp.Channel, queueName string, msg amqp.Publishing) error {
	return ch.PublishWithContext(ctx, "", queueName+".dlq", false, false, msg)
}
