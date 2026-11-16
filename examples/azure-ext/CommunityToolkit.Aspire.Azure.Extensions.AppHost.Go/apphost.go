package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)

	storage := builder.AddAzureStorage("azure-storage").RunAsEmulator(&aspire.RunAsEmulatorOptions{
		ConfigureContainer: func(azurite aspire.AzureStorageEmulatorResource) {
			azurite.WithArgs([]string{"--disableProductStyleUrl"}).
				WithBlobPort(27000).
				WithQueuePort(27001).
				WithTablePort(27002).
				WithDataVolume(&aspire.WithDataVolumeOptions{Name: aspire.StringPtr("storage")})
			check(azurite.Err())
		},
	})
	check(storage.Err())
	blobs := storage.AddBlobs("blobs").WithAzureStorageExplorer()
	check(blobs.Err())

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
