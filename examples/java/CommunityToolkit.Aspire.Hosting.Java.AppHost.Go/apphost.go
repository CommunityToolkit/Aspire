package main

import (
	"apphost/modules/aspire"
	"log"
	"path/filepath"
	"runtime"
)

func main() {
	builder, err := aspire.CreateBuilder()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	projectDirectory, err := filepath.Abs("../CommunityToolkit.Aspire.Hosting.Java.Spring.Maven")
	if err != nil {
		log.Fatal(err)
	}
	jarPath := filepath.Join("target", "spring-maven-0.0.1-SNAPSHOT.jar")
	mavenWrapper, gradleWrapper := "mvnw", "gradlew"
	if runtime.GOOS == "windows" {
		mavenWrapper, gradleWrapper = "mvnw.cmd", "gradlew.bat"
	}

	container := builder.AddJavaContainerApp("java-container", "docker.io/aliencube/aspire-spring-maven-sample",
		&aspire.AddJavaContainerAppOptions{ImageTag: aspire.StringPtr("latest")}).
		WithJvmArgs([]string{"-Djava.awt.headless=true"}).
		WithOtelAgent().
		WithHttpEndpoint(&aspire.WithHttpEndpointOptions{TargetPort: aspire.Float64Ptr(8080), Env: aspire.StringPtr("SERVER_PORT")}).
		WithHttpHealthCheck(&aspire.WithHttpHealthCheckOptions{Path: aspire.StringPtr("/")}).
		WithExplicitStart()
	container.SetEntrypoint(aspire.StringPtr("java"))
	_, err = container.Entrypoint()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	container.SetShellExecution(aspire.BoolPtr(false))
	if _, err := container.ShellExecution(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if _, err := container.Name(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	jar := builder.AddJavaAppWithJar("java-jar", projectDirectory, jarPath,
		&aspire.AddJavaAppWithJarOptions{Args: []string{"--spring.main.banner-mode=off"}}).
		WithWrapperPath(mavenWrapper).
		WithMavenBuild([]string{}).
		WithJvmArgs([]string{"-Duser.timezone=UTC"}).
		WithOtelAgent().
		WithExplicitStart()
	if _, err := jar.Command(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if _, err := jar.WorkingDirectory(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if _, err := jar.Name(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if _, err := jar.JarPath(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	jar.SetJarPath(aspire.StringPtr(jarPath))
	if _, err := jar.JarPath(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	mavenGoal := builder.AddJavaApp("java-maven-goal", projectDirectory).
		WithWrapperPath(mavenWrapper).
		WithMavenGoal("spring-boot:run", []string{}).
		WithJvmArgs([]string{"-Dfile.encoding=UTF-8"}).
		WithOtelAgent().
		WithHttpEndpoint(&aspire.WithHttpEndpointOptions{TargetPort: aspire.Float64Ptr(8080), Env: aspire.StringPtr("SERVER_PORT")}).
		WithHttpHealthCheck(&aspire.WithHttpHealthCheckOptions{Path: aspire.StringPtr("/health")}).
		WithExplicitStart()
	if _, err := mavenGoal.Command(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if _, err := mavenGoal.WorkingDirectory(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if _, err := mavenGoal.Name(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	gradleBuild := builder.AddJavaApp("java-gradle-build", projectDirectory).
		WithWrapperPath(gradleWrapper).
		WithGradleBuild([]string{"--parallel"}).
		WithExplicitStart()
	gradleTask := builder.AddJavaApp("java-gradle-task", projectDirectory).
		WithWrapperPath(gradleWrapper).
		WithGradleTask("bootRun", []string{"--args=--spring.main.banner-mode=off"}).
		WithExplicitStart()

	for _, resource := range []interface{ Err() error }{container, jar, mavenGoal, gradleBuild, gradleTask} {
		if err := resource.Err(); err != nil {
			log.Fatal(aspire.FormatError(err))
		}
	}
	app, err := builder.Build()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := app.Run(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
