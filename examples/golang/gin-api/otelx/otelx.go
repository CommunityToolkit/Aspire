package otelx

import (
	"context"
	"errors"
	"fmt"
	"time"

	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/exporters/otlp/otlpmetric/otlpmetricgrpc"
	"go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracegrpc"
	"go.opentelemetry.io/otel/propagation"
	sdkmetric "go.opentelemetry.io/otel/sdk/metric"
	"go.opentelemetry.io/otel/sdk/resource"
	sdktrace "go.opentelemetry.io/otel/sdk/trace"
	semconv "go.opentelemetry.io/otel/semconv/v1.4.0"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials"
)

var Shutdown = func(context.Context) error {
	return nil
}

func SetupOTelSDK(ctx context.Context, target string, headers map[string]string, name string) (err error) {
	var shutdownFuncs []func(context.Context) error
	Shutdown = func(ctx context.Context) error {
		for _, fn := range shutdownFuncs {
			err = errors.Join(err, fn(ctx))
		}
		shutdownFuncs = nil
		return err
	}
	res, err := resource.New(ctx, resource.WithAttributes(semconv.ServiceNameKey.String(name)))
	if err != nil {
		return fmt.Errorf("failed to create resource: %w", err)
	}
	conn, err := initConn(target)
	if err != nil {
		return err
	}
	tracerProvider, err := newTracer(ctx, res, conn, headers)
	if err != nil {
		return err
	}
	shutdownFuncs = append(shutdownFuncs, tracerProvider.Shutdown)
	meterProvider, err := newMeter(ctx, res, conn, headers)
	if err != nil {
		return err
	}
	shutdownFuncs = append(shutdownFuncs, meterProvider.Shutdown)
	return nil
}

func initConn(target string) (*grpc.ClientConn, error) {
	conn, err := grpc.NewClient(target, grpc.WithTransportCredentials(credentials.NewClientTLSFromCert(nil, "")))
	if err != nil {
		return nil, fmt.Errorf("failed to create gRPC client: %w", err)
	}
	return conn, nil
}

func newTracer(
	ctx context.Context,
	res *resource.Resource,
	conn *grpc.ClientConn,
	headers map[string]string,
) (*sdktrace.TracerProvider, error) {
	exporter, err := otlptracegrpc.New(ctx, otlptracegrpc.WithGRPCConn(conn), otlptracegrpc.WithHeaders(headers))
	if err != nil {
		return nil, fmt.Errorf("failed to create the Jaeger exporter: %w", err)
	}
	processor := sdktrace.NewBatchSpanProcessor(exporter)
	provider := sdktrace.NewTracerProvider(
		sdktrace.WithSampler(sdktrace.AlwaysSample()),
		sdktrace.WithResource(res),
		sdktrace.WithSpanProcessor(processor),
	)
	otel.SetTracerProvider(provider)
	otel.SetTextMapPropagator(propagation.NewCompositeTextMapPropagator(
		propagation.TraceContext{},
		propagation.Baggage{},
	))
	return provider, nil
}

func newMeter(
	ctx context.Context,
	res *resource.Resource,
	conn *grpc.ClientConn,
	headers map[string]string,
) (p *sdkmetric.MeterProvider, err error) {
	exporter, err := otlpmetricgrpc.New(ctx, otlpmetricgrpc.WithGRPCConn(conn), otlpmetricgrpc.WithHeaders(headers))
	if err != nil {
		return nil, fmt.Errorf("failed to create the OTLP exporter: %w", err)
	}
	provider := sdkmetric.NewMeterProvider(
		sdkmetric.WithReader(sdkmetric.NewPeriodicReader(exporter, sdkmetric.WithInterval(3*time.Second))),
		sdkmetric.WithResource(res),
	)
	otel.SetMeterProvider(provider)
	return provider, nil
}
