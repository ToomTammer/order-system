package main

import (
	"context"
	"log/slog"
	"net/http"
	"os"
	"strconv"
	"strings"
	"time"

	amqp "github.com/rabbitmq/amqp091-go"

	"inventory-service/internal/api"
	"inventory-service/internal/application"
	otelsetup "inventory-service/internal/infrastructure/otel"
	"inventory-service/internal/infrastructure/postgres"
	"inventory-service/internal/infrastructure/rabbitmq"
)

// @title		Inventory Service API
// @version		1.0
// @description	HTTP API for inventory-service. Stock reservation itself is driven by RabbitMQ events (order.created); this HTTP surface currently only exposes health checks.
// @BasePath	/
func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))
	slog.SetDefault(logger)

	appEnv := getEnv("APP_ENV", "development")
	rabbitHost := getEnv("RABBITMQ_HOST", "localhost")
	rabbitPort := getEnvInt("RABBITMQ_PORT", 5672)
	rabbitUser := getEnv("RABBITMQ_USER", "guest")
	rabbitPassword := getEnv("RABBITMQ_PASSWORD", "guest")
	httpPort := getEnv("HTTP_PORT", "8080")
	postgresConnString := buildPostgresConnString()

	guardAgainstDevSecretsOutsideDevelopment(appEnv, rabbitPassword, getEnv("POSTGRES_PASSWORD", "postgres"))

	ctx := context.Background()

	shutdownOtel, err := otelsetup.Setup(ctx, "inventory-service")
	if err != nil {
		slog.Error("failed to set up opentelemetry", "error", err)
		os.Exit(1)
	}
	defer shutdownOtel(ctx)

	slog.Info("connecting to postgres")
	pgPool, err := postgres.Connect(ctx, postgresConnString, 10, 2*time.Second)
	if err != nil {
		slog.Error("failed to connect to postgres", "error", err)
		os.Exit(1)
	}
	defer pgPool.Close()
	slog.Info("postgres connected")

	store := postgres.NewReservationStore(pgPool)
	rabbitStatus := &rabbitmq.ConnectionStatus{}

	router := api.NewRouter(appEnv, pgPool, rabbitStatus)
	go func() {
		slog.Info("starting http server", "port", httpPort)
		if err := http.ListenAndServe(":"+httpPort, router); err != nil {
			slog.Error("http server failed", "error", err)
			os.Exit(1)
		}
	}()

	for {
		slog.Info("connecting to rabbitmq", "host", rabbitHost, "port", rabbitPort)
		conn, err := rabbitmq.Connect(rabbitHost, rabbitPort, rabbitUser, rabbitPassword, 10, 2*time.Second)
		if err != nil {
			slog.Error("failed to connect to rabbitmq", "error", err)
			os.Exit(1)
		}
		slog.Info("rabbitmq connected, orders exchange declared")
		rabbitStatus.Set(true)

		publisher, err := rabbitmq.NewPublisher(conn)
		if err != nil {
			slog.Error("failed to open publisher channel", "error", err)
			rabbitStatus.Set(false)
			conn.Close()
			time.Sleep(2 * time.Second)
			continue
		}

		useCase := application.NewReserveStockUseCase(store, publisher)

		closeNotify := conn.NotifyClose(make(chan *amqp.Error, 1))
		go func() {
			if err := rabbitmq.ConsumeOrderCreated(conn, useCase); err != nil {
				slog.Error("consumer stopped", "error", err)
			}
		}()

		closeErr := <-closeNotify
		rabbitStatus.Set(false)
		slog.Warn("rabbitmq connection closed, reconnecting", "error", closeErr)
		time.Sleep(2 * time.Second)
	}
}

func guardAgainstDevSecretsOutsideDevelopment(appEnv, rabbitPassword, postgresPassword string) {
	if strings.EqualFold(appEnv, "development") || strings.EqualFold(appEnv, "local") {
		return
	}
	if rabbitPassword == "guest" {
		slog.Error("refusing to start: RABBITMQ_PASSWORD is still the default \"guest\" outside a development environment; set a real credential via your deploy target's secrets manager")
		os.Exit(1)
	}
	if postgresPassword == "postgres" {
		slog.Error("refusing to start: POSTGRES_PASSWORD is still the docker-compose dev default outside a development environment; set a real credential via your deploy target's secrets manager")
		os.Exit(1)
	}
}

func buildPostgresConnString() string {
	host := getEnv("POSTGRES_HOST", "localhost")
	port := getEnv("POSTGRES_PORT", "5432")
	db := getEnv("POSTGRES_DB", "inventory_db")
	user := getEnv("POSTGRES_USER", "postgres")
	password := getEnv("POSTGRES_PASSWORD", "postgres")
	return "postgres://" + user + ":" + password + "@" + host + ":" + port + "/" + db
}

func getEnv(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}

func getEnvInt(key string, fallback int) int {
	if v := os.Getenv(key); v != "" {
		if n, err := strconv.Atoi(v); err == nil {
			return n
		}
	}
	return fallback
}
