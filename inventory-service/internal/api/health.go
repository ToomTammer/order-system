package api

import (
	"net/http"

	"github.com/gin-gonic/gin"
	"github.com/jackc/pgx/v5/pgxpool"

	"inventory-service/internal/infrastructure/rabbitmq"
)

func registerHealthRoutes(router *gin.Engine, pgPool *pgxpool.Pool, rabbitStatus *rabbitmq.ConnectionStatus) {
	router.GET("/health/live", healthLiveHandler)
	router.GET("/health/ready", healthReadyHandler(pgPool, rabbitStatus))
}

// healthLiveHandler godoc
//
//	@Summary	Liveness probe
//	@Description	Returns 200 once the process is up. Does not check any downstream dependency.
//	@Tags		health
//	@Produce	json
//	@Success	200	{object}	map[string]string	"status: live"
//	@Router		/health/live [get]
func healthLiveHandler(c *gin.Context) {
	c.JSON(http.StatusOK, gin.H{"status": "live"})
}

// healthReadyHandler godoc
//
//	@Summary	Readiness probe
//	@Description	Returns 200 only if both Postgres and RabbitMQ are reachable; 503 otherwise.
//	@Tags		health
//	@Produce	json
//	@Success	200	{object}	map[string]string	"status: ready"
//	@Failure	503	{object}	map[string]string	"status: not ready, reason: ..."
//	@Router		/health/ready [get]
func healthReadyHandler(pgPool *pgxpool.Pool, rabbitStatus *rabbitmq.ConnectionStatus) gin.HandlerFunc {
	return func(c *gin.Context) {
		if err := pgPool.Ping(c.Request.Context()); err != nil {
			c.JSON(http.StatusServiceUnavailable, gin.H{"status": "not ready", "reason": "postgres: " + err.Error()})
			return
		}
		if !rabbitStatus.IsConnected() {
			c.JSON(http.StatusServiceUnavailable, gin.H{"status": "not ready", "reason": "rabbitmq: not connected"})
			return
		}
		c.JSON(http.StatusOK, gin.H{"status": "ready"})
	}
}
