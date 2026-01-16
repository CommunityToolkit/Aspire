package main

import (
	"context"
	"log"
	"main/otelx"
	"net/http"
	"os"
	"strings"
	"time"

	"github.com/gin-gonic/gin"
	"go.opentelemetry.io/contrib/instrumentation/github.com/gin-gonic/gin/otelgin"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/metric"
)

var db = make(map[string]string)

var (
	name                  = os.Getenv("OTEL_SERVICE_NAME")
	isInsecure            bool
	otelTarget            string
	headers               map[string]string
	meter                 metric.Meter
	metricRequestTotal    metric.Int64Counter
	responseTimeHistogram metric.Int64Histogram
)

func setupRouter() *gin.Engine {
	// Disable Console Color
	// gin.DisableConsoleColor()

	r := gin.Default()
	r.Use(otelgin.Middleware(name))
	r.Use(monitorInterceptor())

    r.GET("/health", func(c *gin.Context) {
        c.String(http.StatusOK, "ok")
    })

	// Serve static files from /static directory
	// When using container files feature, these files will be copied from
	// a frontend resource into the container at /app/static
	r.Static("/static", "./static")

	// Ping test
	r.GET("/ping", func(c *gin.Context) {
		// _, span := tracer.Start(c.Request.Context(), "ping")
		// defer span.End()
		c.String(http.StatusOK, "pong")
	})

	// Get user value
	r.GET("/user/:name", func(c *gin.Context) {
		user := c.Params.ByName("name")
		value, ok := db[user]
		if ok {
			c.JSON(http.StatusOK, gin.H{"user": user, "value": value})
		} else {
			c.JSON(http.StatusOK, gin.H{"user": user, "status": "no value"})
		}
	})

	// Authorized group (uses gin.BasicAuth() middleware)
	// Same than:
	// authorized := r.Group("/")
	// authorized.Use(gin.BasicAuth(gin.Credentials{
	//	  "foo":  "bar",
	//	  "manu": "123",
	//}))
	authorized := r.Group("/", gin.BasicAuth(gin.Accounts{
		"foo":  "bar", // user:foo password:bar
		"manu": "123", // user:manu password:123
	}))

	/* example curl for /admin with basicauth header
	   Zm9vOmJhcg== is base64("foo:bar")

		curl -X POST \
	  	http://localhost:8080/admin \
	  	-H 'authorization: Basic Zm9vOmJhcg==' \
	  	-H 'content-type: application/json' \
	  	-d '{"value":"bar"}'
	*/
	authorized.POST("admin", func(c *gin.Context) {
		user := c.MustGet(gin.AuthUserKey).(string)

		// Parse JSON
		var json struct {
			Value string `json:"value" binding:"required"`
		}

		if c.Bind(&json) == nil {
			db[user] = json.Value
			c.JSON(http.StatusOK, gin.H{"status": "ok"})
		}
	})

	return r
}

func main() {
	otelEndpoint := strings.Split(os.Getenv("OTEL_EXPORTER_OTLP_ENDPOINT"), "https://")
	if len(otelEndpoint) > 1 {
		isInsecure = false
		otelTarget = otelEndpoint[1]
	} else {
		isInsecure = true
		otelTarget = strings.Split(os.Getenv("OTEL_EXPORTER_OTLP_ENDPOINT"), "http://")[1]
	}
	otelHeaders := strings.Split(os.Getenv("OTEL_EXPORTER_OTLP_HEADERS"), "=")
	if len(otelHeaders) > 1 {
		headers = map[string]string{otelHeaders[0]: otelHeaders[1]}
	}
	// Initialize OpenTelemetry
	err := otelx.SetupOTelSDK(context.Background(), otelTarget, isInsecure, headers, name)
	if err != nil {
		log.Printf("Failed to initialize OpenTelemetry: %v", err)
		return
	}
	defer func() {
		err = otelx.Shutdown(context.Background())
		if err != nil {
			log.Printf("Failed to shutdown OpenTelemetry: %v", err)
		}
	}()

	// Create a tracer and a meter
	meter = otel.Meter(name)
	initGinMetrics()

	r := setupRouter()

	// Listen and Server in 0.0.0.0:8080
	r.Run(":"+os.Getenv("PORT"))
}

func initGinMetrics() {

	metricRequestTotal, _ = meter.Int64Counter("gin_request_total",
		metric.WithDescription("all the server received request num."),
	)

	// Create a histogram to measure response time
	responseTimeHistogram, _ = meter.Int64Histogram("gin_response_time",
		metric.WithDescription("The distribution of response times."),
	)
}

// monitorInterceptor as gin monitor middleware.
func monitorInterceptor() gin.HandlerFunc {
	return func(c *gin.Context) {
		startTime := time.Now()

		// execute normal process.
		c.Next()

		// after request
		ginMetricHandle(c.Request.Context(), startTime)
	}
}

func ginMetricHandle(c context.Context, start time.Time) {
	// set request total
	metricRequestTotal.Add(c, 1)

	// Record the response time
	duration := time.Since(start)
	responseTimeHistogram.Record(c, duration.Milliseconds())
}
