package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)

	stateStore := builder.AddDaprStateStore("statestore")
	pubSub := builder.AddDaprPubSub("pubsub")
	check(stateStore.Err())
	check(pubSub.Err())

	serviceA := builder.AddDotnetProject("servicea", "../CommunityToolkit.Aspire.Hosting.Dapr.ServiceA/CommunityToolkit.Aspire.Hosting.Dapr.ServiceA.csproj").
		ConfigureDaprSidecar(func(sidecar aspire.DaprSidecarResource) {
			check(sidecar.WithReference(stateStore).WithReference(pubSub).Err())
		})
	check(serviceA.Err())
	serviceB := builder.AddDotnetProject("serviceb", "../CommunityToolkit.Aspire.Hosting.Dapr.ServiceB/CommunityToolkit.Aspire.Hosting.Dapr.ServiceB.csproj").
		ConfigureDaprSidecar(func(sidecar aspire.DaprSidecarResource) {
			check(sidecar.WithReference(pubSub).Err())
		}).WaitFor(serviceA)
	check(serviceB.Err())
	serviceC := builder.AddDotnetProject("servicec", "../CommunityToolkit.Aspire.Hosting.Dapr.ServiceC/CommunityToolkit.Aspire.Hosting.Dapr.ServiceC.csproj").
		ConfigureDaprSidecar(func(sidecar aspire.DaprSidecarResource) {
			check(sidecar.WithReference(stateStore).Err())
		}).WaitFor(serviceA)
	check(serviceC.Err())

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
