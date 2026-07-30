package api

import (
	"strings"

	"github.com/gin-gonic/gin"
	"github.com/jackc/pgx/v5/pgxpool"
	swaggerFiles "github.com/swaggo/files"
	ginSwagger "github.com/swaggo/gin-swagger"

	_ "inventory-service/docs"
	"inventory-service/internal/infrastructure/rabbitmq"
)

func NewRouter(appEnv string, pgPool *pgxpool.Pool, rabbitStatus *rabbitmq.ConnectionStatus) *gin.Engine {
	isDev := strings.EqualFold(appEnv, "development") || strings.EqualFold(appEnv, "local")
	if !isDev {
		gin.SetMode(gin.ReleaseMode)
	}

	router := gin.New()
	router.Use(gin.Recovery())
	router.Use(EnforceHTTPS(appEnv))

	registerHealthRoutes(router, pgPool, rabbitStatus)

	if isDev {
		router.GET("/swagger/*any", ginSwagger.WrapHandler(swaggerFiles.Handler))
	}

	return router
}
