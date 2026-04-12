using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;

namespace CommunityToolkit.Aspire.Hosting.Compose.Generator;

/// <summary>
/// Incremental source generator that reads Docker Compose files registered via
/// <c>&lt;ComposeReference&gt;</c> MSBuild items and generates strongly-typed
/// wrapper classes with service properties.
/// </summary>
[Generator]
public sealed class ComposeSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ComposeFile> composeFiles = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (pair, ct) =>
            {
                (AdditionalText? file, AnalyzerConfigOptionsProvider? optionsProvider) = pair;
                AnalyzerConfigOptions options = optionsProvider.GetOptions(file);

                if (!options.TryGetValue("build_metadata.AdditionalFiles.ComposeReferenceName", out string? name) || string.IsNullOrEmpty(name))
                    return default;

                string path = file.Path;

                if (!path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                    return default;

                string? content = file.GetText(ct)?.ToString();
                return new ComposeFile(name, path, content);
            })
            .Where(static info => info.Name is not null);

        context.RegisterSourceOutput(composeFiles, static (ctx, info) =>
        {
            if (info.Content is null)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(Diagnostics.ComposeFileNotFound, Location.None, info.Path));

                string emptySource = ComposeClassEmitter.EmitClass(info.Name!, info.Path!, []);
                ctx.AddSource($"Compose.{info.Name}.g.cs", SourceText.From(emptySource, Encoding.UTF8));
                return;
            }

            List<string> serviceNames = ComposeServiceNameExtractor.Extract(info.Content);
            string source = ComposeClassEmitter.EmitClass(info.Name!, info.Path!, serviceNames.ToArray());
            ctx.AddSource($"Compose.{info.Name}.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    internal readonly struct ComposeFile(string name, string path, string? content)
    {
        public readonly string? Name = name;
        public readonly string? Path = path;
        public readonly string? Content = content;
    }
}
