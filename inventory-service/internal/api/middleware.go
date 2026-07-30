package api

import (
	"net/http"
	"strings"

	"github.com/gin-gonic/gin"
)

func EnforceHTTPS(appEnv string) gin.HandlerFunc {
	isDev := strings.EqualFold(appEnv, "development") || strings.EqualFold(appEnv, "local")
	return func(c *gin.Context) {
		if !isDev && !strings.HasPrefix(c.Request.URL.Path, "/health") && c.GetHeader("X-Forwarded-Proto") != "https" {
			c.String(http.StatusBadRequest, "HTTPS is required outside local development.")
			c.Abort()
			return
		}
		c.Next()
	}
}
