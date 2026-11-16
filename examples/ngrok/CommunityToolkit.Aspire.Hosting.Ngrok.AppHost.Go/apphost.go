package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	authToken := builder.AddParameter("ngrok-auth-token", &aspire.AddParameterOptions{
		Value: aspire.StringPtr("ngrok-token-value"), Secret: aspire.BoolPtr(true),
	})
	upstream := builder.AddContainer("upstream", "nginx:alpine").
		WithHttpEndpoint(&aspire.WithHttpEndpointOptions{Name: aspire.StringPtr("http"), TargetPort: aspire.Float64Ptr(80)})
	parameter := builder.AddNgrok("ngrok-parameter", &aspire.AddNgrokOptions{
		ConfigurationFolder: aspire.StringPtr(".ngrok-parameter"), EndpointPort: aspire.Float64Ptr(59600),
		EndpointName: aspire.StringPtr("http"), ConfigurationVersion: aspire.Float64Ptr(3),
	}).WithAuthToken(authToken).WithTunnelEndpoint(upstream, "http", &aspire.WithTunnelEndpointOptions{
		NgrokUrl: aspire.StringPtr("https://parameter.ngrok.test"),
		Labels:   map[string]string{"environment": "dev", "service": "upstream"},
	})
	value := builder.AddNgrok("ngrok-value").WithAuthTokenValue("plain-text-token").WithTunnelEndpoint(upstream, "http")
	if err := parameter.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := value.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	app, err := builder.Build()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := app.Run(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
