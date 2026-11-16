package main

import (
	"apphost/modules/aspire"
	"fmt"
	"log"
	"path/filepath"
)

func configureWait(options aspire.CrdWaitOptions, timeout float64) error {
	options.SetTimeout(timeout).SetFailureBehavior(aspire.CrdWaitBehaviorBestEffort)
	if err := options.Err(); err != nil {
		return err
	}
	actualTimeout, err := options.Timeout()
	if err != nil {
		return err
	}
	behavior, err := options.FailureBehavior()
	if err != nil {
		return err
	}
	if actualTimeout != timeout || behavior != aspire.CrdWaitBehaviorBestEffort {
		return fmt.Errorf("CRD-wait options did not round-trip through the generated SDK")
	}
	return nil
}

func main() {
	builder, err := aspire.CreateBuilder()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	cluster := builder.AddKindCluster("ts-kind")
	manifestConfigured, chartConfigured := false, false
	manifest := cluster.AddManifestFromContent("ts-manifest", `
apiVersion: v1
kind: Namespace
metadata:
  name: ts-crd-wait
`)
	manifest.WithCrdWait(func(options aspire.CrdWaitOptions) {
		if err := configureWait(options, 120_000); err != nil {
			log.Fatal(aspire.FormatError(err))
		}
		manifestConfigured = true
	})
	chartPath, err := filepath.Abs("../CommunityToolkit.Aspire.Hosting.Kind.AppHost.TypeScript/chart")
	if err != nil {
		log.Fatal(err)
	}
	chart := cluster.AddHelmChart("ts-chart", chartPath)
	chart.WithCrdWait(func(options aspire.CrdWaitOptions) {
		if err := configureWait(options, 180_000); err != nil {
			log.Fatal(aspire.FormatError(err))
		}
		chartConfigured = true
	})
	if !manifestConfigured || !chartConfigured {
		log.Fatal("CRD-wait callbacks were not invoked for both Kind builders")
	}
	timeout, err := manifest.CrdWaitTimeout()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	behavior, err := manifest.CrdWaitBehavior()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if timeout != 120_000 || behavior != aspire.CrdWaitBehaviorBestEffort {
		log.Fatal("Manifest CRD-wait policy did not retain the configured options")
	}
	manifest.WithCrdWaitTimeout(90_000).WithCrdWaitBehavior(aspire.CrdWaitBehaviorFail)
	timeout, err = manifest.CrdWaitTimeout()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	behavior, err = manifest.CrdWaitBehavior()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if timeout != 90_000 || behavior != aspire.CrdWaitBehaviorFail {
		log.Fatal("Legacy manifest CRD-wait methods did not update the configured options")
	}
	if err := chart.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := manifest.Err(); err != nil {
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
