using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;

namespace Aspire.Hosting;

public static partial class JavaAppHostingExtension
{
    private const string DefaultJavaBuildImage = "eclipse-temurin:21-jdk";
    private const string DefaultJavaRuntimeImage = "eclipse-temurin:21-jre";

#pragma warning disable ASPIREDOCKERFILEBUILDER001
    private static IResourceBuilder<JavaAppExecutableResource> PublishAsJavaDockerfile(
        this IResourceBuilder<JavaAppExecutableResource> builder)
    {
        return builder.PublishAsDockerFile(publish =>
        {
            if (File.Exists(Path.Combine(builder.Resource.WorkingDirectory, "Dockerfile")))
            {
                return;
            }

            publish.WithDockerfileBuilder(builder.Resource.WorkingDirectory, context =>
            {
                JavaAppExecutableResource resource = (JavaAppExecutableResource)context.Resource;
                context.Resource.TryGetLastAnnotation<DockerfileBaseImageAnnotation>(out var baseImageAnnotation);

                string buildImage = baseImageAnnotation?.BuildImage ?? DefaultJavaBuildImage;
                string runtimeImage = baseImageAnnotation?.RuntimeImage ?? DefaultJavaRuntimeImage;
                resource.TryGetLastAnnotation<JavaPublishBuildAnnotation>(out var publishBuildAnnotation);

                var buildStage = context.Builder
                    .From(buildImage, "builder")
                    .WorkDir("/workspace")
                    .Copy(".", "./");

                if (publishBuildAnnotation is not null)
                {
                    buildStage.Run(GetPublishBuildCommand(resource, publishBuildAnnotation));
                }

                buildStage.Run(GetPublishArtifactCommand(resource, publishBuildAnnotation));

                context.Builder
                    .From(runtimeImage, "app")
                    .WorkDir("/app")
                    .CopyFrom(buildStage.StageName!, "/out/app.jar", "/app/app.jar")
                    .Entrypoint(["java", "-jar", "/app/app.jar"]);
            });
        });
    }
#pragma warning restore ASPIREDOCKERFILEBUILDER001

    private static string GetPublishBuildCommand(
        JavaAppExecutableResource resource,
        JavaPublishBuildAnnotation publishBuildAnnotation)
    {
        string wrapperPath = publishBuildAnnotation.WrapperPath is not null
            ? GetPathRelativeToWorkingDirectory(resource.WorkingDirectory, publishBuildAnnotation.WrapperPath, "wrapper script")
            : publishBuildAnnotation.Tool switch
            {
                JavaBuildTool.Maven => "mvnw",
                JavaBuildTool.Gradle => "gradlew",
                _ => throw new InvalidOperationException($"Unsupported Java build tool '{publishBuildAnnotation.Tool}'.")
            };

        if (wrapperPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
            wrapperPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Java publish requires a Unix-compatible wrapper path, but '{wrapperPath}' points to a Windows wrapper.");
        }

        if (!wrapperPath.StartsWith("./", StringComparison.Ordinal) &&
            !wrapperPath.StartsWith("/", StringComparison.Ordinal))
        {
            wrapperPath = $"./{wrapperPath}";
        }

        string quotedWrapperPath = QuoteShellArgument(wrapperPath);
        IEnumerable<string> quotedArgs = publishBuildAnnotation.Args.Select(QuoteShellArgument);

        return $"chmod +x {quotedWrapperPath} && {quotedWrapperPath} {string.Join(" ", quotedArgs)}";
    }

    private static string GetPublishArtifactCommand(
        JavaAppExecutableResource resource,
        JavaPublishBuildAnnotation? publishBuildAnnotation)
    {
        if (resource.JarPath is not null)
        {
            string jarPath = GetPathRelativeToWorkingDirectory(resource.WorkingDirectory, resource.JarPath, "JAR path");
            return $"mkdir -p /out && cp {QuoteShellArgument(jarPath)} /out/app.jar";
        }

        if (publishBuildAnnotation is null)
        {
            throw new InvalidOperationException($"Java publish for resource '{resource.Name}' requires either a JAR path or a Maven/Gradle build configuration.");
        }

        string searchDirectory = publishBuildAnnotation.Tool switch
        {
            JavaBuildTool.Maven => "target",
            JavaBuildTool.Gradle => "build/libs",
            _ => throw new InvalidOperationException($"Unsupported Java build tool '{publishBuildAnnotation.Tool}'.")
        };

        return
            $"mkdir -p /out && artifact=$(find {QuoteShellArgument(searchDirectory)} -maxdepth 1 -type f -name '*.jar' ! -name '*-sources.jar' ! -name '*-javadoc.jar' ! -name '*-tests.jar' ! -name '*-plain.jar' ! -name 'original-*.jar' | sort | head -n 1) && test -n \"$artifact\" && cp \"$artifact\" /out/app.jar";
    }

    private static string GetPathRelativeToWorkingDirectory(
        string workingDirectory,
        string path,
        string description)
    {
        string fullPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workingDirectory, path));

        string relativePath = Path.GetRelativePath(workingDirectory, fullPath);

        if (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"Java publish requires the {description} '{path}' to be inside the application's working directory.");
        }

        return NormalizeContainerPath(relativePath);
    }

    private static string NormalizeContainerPath(string path) =>
        path.Replace('\\', '/');

    private static string QuoteShellArgument(string value) =>
        $"'{value.Replace("'", "'\"'\"'")}'";
}
