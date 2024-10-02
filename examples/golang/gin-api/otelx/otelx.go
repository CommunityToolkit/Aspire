package otelx

import (
	"context"
	"errors"
	"fmt"
	"time"

	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/exporters/otlp/otlpmetric/otlpmetricgrpc"
	"go.opentelemetry.io/otel/exporters/otlp/otlptrace"
	"go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracegrpc"
	"go.opentelemetry.io/otel/propagation"
	sdkmetric "go.opentelemetry.io/otel/sdk/metric"
	"go.opentelemetry.io/otel/sdk/resource"
	sdktrace "go.opentelemetry.io/otel/sdk/trace"
	semconv "go.opentelemetry.io/otel/semconv/v1.4.0"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials"
	"google.golang.org/grpc/credentials/insecure"
)

var Shutdown = func(context.Context) error {
	return nil
}

func SetupOTelSDK(ctx context.Context, target string, isInsecure bool, headers map[string]string, name string) (err error) {
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
	conn, err := initConn(target, isInsecure)
	if err != nil {
		return err
	}
	tracerProvider, err := newTracer(ctx, res, conn, headers, isInsecure)
	if err != nil {
		return err
	}
	shutdownFuncs = append(shutdownFuncs, tracerProvider.Shutdown)
	meterProvider, err := newMeter(ctx, res, conn, headers, isInsecure)
	if err != nil {
		return err
	}
	shutdownFuncs = append(shutdownFuncs, meterProvider.Shutdown)
	return nil
}

func initConn(target string, isInsecure bool) (*grpc.ClientConn, error) {
	var conn *grpc.ClientConn
	var err error
	if isInsecure {
		conn, err = grpc.NewClient(target, grpc.WithTransportCredentials(insecure.NewCredentials()))

	} else {
		conn, err = grpc.NewClient(target, grpc.WithTransportCredentials(credentials.NewClientTLSFromCert(nil, "")))
	}

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
	isInsecure bool,
) (*sdktrace.TracerProvider, error) {
	var exporter *otlptrace.Exporter
	var err error
	if isInsecure {
		exporter, err = otlptracegrpc.New(ctx, otlptracegrpc.WithGRPCConn(conn))
	} else {
		exporter, err = otlptracegrpc.New(ctx, otlptracegrpc.WithGRPCConn(conn), otlptracegrpc.WithHeaders(headers))
	}
	if err != nil {
		return nil, fmt.Errorf("failed to create the OTLP exporter: %w", err)
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
	isInsecure bool,
) (*sdkmetric.MeterProvider, error) {
	var exporter *otlpmetricgrpc.Exporter
	var err error
	if isInsecure {
		exporter, err = otlpmetricgrpc.New(ctx, otlpmetricgrpc.WithGRPCConn(conn))
	} else {
		exporter, err = otlpmetricgrpc.New(ctx, otlpmetricgrpc.WithGRPCConn(conn), otlpmetricgrpc.WithHeaders(headers))
	}
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
