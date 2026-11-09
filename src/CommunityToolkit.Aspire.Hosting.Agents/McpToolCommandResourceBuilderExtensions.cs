// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Aspire.Hosting;

#pragma warning disable ASPIREINTERACTION001 // IInteractionService is used to prompt for dashboard command input.

/// <summary>
/// Provides extension methods for invoking MCP (Model Context Protocol) tools on resources.
/// </summary>
public static class McpToolCommandResourceBuilderExtensions
{
    private const string McpToolArgumentName = "tool";
    private const string McpToolArgumentsArgumentName = "arguments";

    /// <summary>
    /// Adds commands for invoking tools on the resource's configured MCP server.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for chaining additional configuration.</returns>
    /// <ats-returns>The resource builder.</ats-returns>
    /// <remarks>
    /// Configure the MCP endpoint with <c>WithMcpServer</c> before calling this method.
    /// Adds an interactive dashboard command, an API command accepting a tool name and JSON arguments,
    /// and an MCP endpoint URL. The interactive command is highlighted only if the resource
    /// does not already have a highlighted command. Endpoint resolution uses the existing
    /// <see cref="McpServerEndpointAnnotation"/> without changing its configuration.
    /// This method has no effect in publish mode.
    /// </remarks>
    /// <example>
    /// Enable tool invocation on a resource hosting an MCP server:
    /// <code>
    /// var api = builder.AddProject&lt;Projects.MyApi&gt;("api")
    ///     .WithMcpServer("/mcp", endpointName: "https")
    ///     .WithMcpToolCommands();
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The resource has no configured MCP server endpoint.</exception>
    [Experimental("CTASPIREAGENTS001", UrlFormat = "https://aka.ms/communitytoolkit/aspire/diagnostics#{0}")]
    [AspireExport]
    public static IResourceBuilder<T> WithMcpToolCommands<T>(this IResourceBuilder<T> builder)
        where T : IResourceWithEndpoints
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder;
        }

        if (!builder.Resource.TryGetLastAnnotation<McpServerEndpointAnnotation>(out var annotation))
        {
            throw new InvalidOperationException(
                $"Resource '{builder.Resource.Name}' has no MCP server endpoint. Call WithMcpServer before WithMcpToolCommands.");
        }

        var interactiveCommandName = $"{builder.Resource.Name}-mcp-call-tool-interactive";
        if (builder.Resource.Annotations.OfType<ResourceCommandAnnotation>().Any(c => string.Equals(c.Name, interactiveCommandName, StringComparison.Ordinal)))
        {
            return builder;
        }

        var isHighlighted = !builder.Resource.Annotations
            .OfType<ResourceCommandAnnotation>()
            .Any(command => command.IsHighlighted);

        AddMcpEndpointUrl(builder, annotation);
        AddMcpInvokeCommandIfMissing(builder, annotation, isHighlighted);

        return builder;
    }

    private static void AddMcpEndpointUrl<T>(IResourceBuilder<T> builder, McpServerEndpointAnnotation annotation)
        where T : IResourceWithEndpoints
    {
        builder.WithUrls(async context =>
        {
            if (context.ExecutionContext.IsPublishMode)
            {
                return;
            }

            Uri? uri;
            try
            {
                uri = await annotation.EndpointUrlResolver(builder.Resource, context.CancellationToken).ConfigureAwait(false);
            }
            catch (DistributedApplicationException ex)
            {
                context.Logger.LogWarning(ex, "Could not add MCP endpoint URL for resource '{ResourceName}'.", builder.Resource.Name);
                return;
            }

            if (uri is null)
            {
                return;
            }

            context.Urls.Add(new ResourceUrlAnnotation
            {
                Url = uri.AbsoluteUri,
                DisplayText = "MCP Endpoint"
            });
        });
    }

    private static void AddMcpInvokeCommandIfMissing<T>(IResourceBuilder<T> builder, McpServerEndpointAnnotation annotation, bool isHighlighted)
        where T : IResourceWithEndpoints
    {
        var interactiveCommandName = $"{builder.Resource.Name}-mcp-call-tool-interactive";
        if (builder.Resource.Annotations.OfType<ResourceCommandAnnotation>().Any(c => string.Equals(c.Name, interactiveCommandName, StringComparison.Ordinal)))
        {
            return;
        }

        builder.ApplicationBuilder.Services.AddHttpClient();

        builder.WithCommand(
            interactiveCommandName,
            "Invoke MCP",
            context => ExecuteMcpToolCallAsync(context, builder.Resource, annotation),
            new CommandOptions
            {
                IconName = "ChatSparkle",
                IconVariant = IconVariant.Regular,
                IsHighlighted = isHighlighted,
                UpdateState = GetMcpCommandState,
                Visibility = ResourceCommandVisibility.UI
            });

        var commandWithArgumentsName = $"{builder.Resource.Name}-mcp-call-tool";
        if (builder.Resource.Annotations.OfType<ResourceCommandAnnotation>().Any(c => string.Equals(c.Name, commandWithArgumentsName, StringComparison.Ordinal)))
        {
            return;
        }

        builder.WithCommand(
            commandWithArgumentsName,
            "Invoke MCP",
            context => ExecuteMcpToolCallAsync(context, builder.Resource, annotation),
            new CommandOptions
            {
                Description = "Invoke an MCP tool by name with JSON arguments.",
                IconName = "ChatSparkle",
                IconVariant = IconVariant.Regular,
                Arguments = [CreateMcpToolArgument(), CreateMcpArgumentsArgument()],
                UpdateState = GetMcpCommandState,
                Visibility = ResourceCommandVisibility.Api
            });
    }

    private static ResourceCommandState GetMcpCommandState(UpdateCommandStateContext context)
    {
        var state = context.ResourceSnapshot.State?.Text;
        return state == KnownResourceStates.Running || state == KnownResourceStates.RuntimeUnhealthy
            ? ResourceCommandState.Enabled
            : ResourceCommandState.Disabled;
    }

    private static async Task<ExecuteCommandResult> ExecuteMcpToolCallAsync(
        ExecuteCommandContext context,
        IResourceWithEndpoints resource,
        McpServerEndpointAnnotation annotation)
    {
        try
        {
            var uri = await annotation.EndpointUrlResolver(resource, context.CancellationToken).ConfigureAwait(true);
            if (uri is null)
            {
                return CommandResults.Failure("The MCP server endpoint URL is not available.");
            }

            if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https"))
            {
                return CommandResults.Failure($"Could not invoke MCP tools for resource '{resource.Name}' as the MCP server URL is not an absolute HTTP or HTTPS URL.");
            }

            using var httpClient = context.Services.GetRequiredService<IHttpClientFactory>().CreateClient(string.Empty);
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
            using var transportClient = new HttpClient(new McpCleanupHandler(
                httpClient, context.Services.GetRequiredService<ResourceLoggerService>().GetLogger(context.ResourceName)))
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            await using var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = uri,
                Name = "Aspire Dashboard",
                TransportMode = HttpTransportMode.StreamableHttp,
                OwnsSession = true
            }, transportClient);
            await using var client = await McpClient.CreateAsync(transport, new McpClientOptions
            {
                ClientInfo = new Implementation { Name = "Aspire Dashboard", Version = "1.0" },
                InitializationTimeout = Timeout.InfiniteTimeSpan
            }, cancellationToken: context.CancellationToken).ConfigureAwait(true);
            var requestContext = new McpToolCommandContext(
                context.Services, context.CancellationToken, context.Arguments);
            var tools = await ReadAllMcpToolsAsync(client, context.CancellationToken).ConfigureAwait(true);
            if (tools.Count == 0)
            {
                throw new InvalidOperationException("MCP server did not return any tools.");
            }

            var (selectedTool, arguments) = await GetMcpToolCallAsync(requestContext, tools).ConfigureAwait(true);
            try
            {
                // Keep the result as JSON so the dashboard preserves unknown content types and metadata.
                var response = await client.SendRequestAsync(new JsonRpcRequest
                {
                    Id = new RequestId(Guid.NewGuid().ToString("N")),
                    Method = "tools/call",
                    Params = new JsonObject { ["name"] = selectedTool.Name, ["arguments"] = arguments }
                }, context.CancellationToken).ConfigureAwait(true);
                context.CancellationToken.ThrowIfCancellationRequested();
                return GetMcpCommandResult(response);
            }
            catch (McpProtocolException ex)
            {
                return CommandResults.Failure(
                    "MCP tool call returned a JSON-RPC error.",
                    JsonSerializer.Serialize(new { code = (int)ex.ErrorCode, message = ex.Message }, s_indentedJsonOptions),
                    CommandResultFormat.Json);
            }
        }
        catch (OperationCanceledException ex) when (ex is McpDialogCanceledException || context.CancellationToken.IsCancellationRequested)
        {
            return CommandResults.Canceled();
        }
        catch (HttpRequestException ex)
        {
            return CommandResults.Failure(
                ex.StatusCode is { } status
                    ? $"MCP request failed with HTTP status {(int)status} ({status})."
                    : "MCP transport request failed.",
                ex.Message,
                CommandResultFormat.Text);
        }
        catch (McpException ex)
        {
            return CommandResults.Failure("MCP protocol request failed.", ex.Message, CommandResultFormat.Text);
        }
        catch (JsonException ex)
        {
            return CommandResults.Failure("MCP server returned invalid JSON.", ex.Message, CommandResultFormat.Text);
        }
        catch (Exception ex)
        {
            return CommandResults.Failure(ex);
        }
    }

    private static async Task<IReadOnlyList<McpTool>> ReadAllMcpToolsAsync(
        McpClient client,
        CancellationToken cancellationToken)
    {
        var tools = new List<McpTool>();
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var page = await client.ListToolsAsync(new ListToolsRequestParams { Cursor = cursor }, cancellationToken).ConfigureAwait(true);
            tools.AddRange(page.Tools.Where(tool => !string.IsNullOrEmpty(tool.Name)).Select(tool =>
                new McpTool(tool.Name, tool.Description, JsonSerializer.SerializeToNode(tool.InputSchema) as JsonObject)));
            cursor = page.NextCursor is { Length: > 0 } nextCursor ? nextCursor : null;

            if (cursor is not null && !seenCursors.Add(cursor))
            {
                throw new InvalidOperationException($"MCP server returned the tools/list cursor '{cursor}' more than once.");
            }
        }
        while (cursor is not null);

        return tools;
    }

    private static InteractionInput CreateMcpToolArgument()
    {
        return new InteractionInput
        {
            Name = McpToolArgumentName,
            Label = "Tool",
            Description = "Name of the MCP tool to invoke.",
            InputType = InputType.Text,
            Required = true,
            Placeholder = "get_weather"
        };
    }

    private static InteractionInput CreateMcpArgumentsArgument()
    {
        return new InteractionInput
        {
            Name = McpToolArgumentsArgumentName,
            Label = "Arguments JSON",
            Description = "JSON object to pass as the MCP tool arguments.",
            InputType = InputType.Text,
            Required = false,
            Value = "{}",
            Placeholder = "{ \"location\": \"Seattle\" }"
        };
    }

    private static ExecuteCommandResult GetMcpCommandResult(JsonRpcResponse response)
    {
        if (response.Result is JsonObject toolResult &&
            toolResult["content"] is JsonArray &&
            (toolResult["isError"] is null || toolResult["isError"] is JsonValue isErrorValue && isErrorValue.TryGetValue<bool>(out _)))
        {
            if (toolResult["isError"]?.GetValue<bool>() is true)
            {
                return CommandResults.Failure(
                    "MCP tool reported an error.",
                    JsonSerializer.Serialize(toolResult, s_indentedJsonOptions),
                    CommandResultFormat.Json);
            }

            return CommandResults.Success(
                message: "MCP tool response received.",
                result: JsonSerializer.Serialize(response, s_indentedJsonOptions),
                resultFormat: CommandResultFormat.Json,
                displayImmediately: true);
        }

        return CommandResults.Failure(
            "MCP server returned an empty or invalid JSON-RPC tool response.",
            JsonSerializer.Serialize(response, s_indentedJsonOptions),
            CommandResultFormat.Json);
    }

    private static McpTool GetSelectedMcpTool(InteractionInputCollection arguments, IReadOnlyList<McpTool> tools)
    {
        var toolName = GetMcpArgumentValue(arguments, McpToolArgumentName)
            ?? throw new InvalidOperationException("MCP tool argument is required.");
        var selectedTool = tools.FirstOrDefault(tool => string.Equals(tool.Name, toolName, StringComparison.Ordinal));
        if (selectedTool is not null)
        {
            return selectedTool;
        }

        var availableTools = string.Join(", ", tools.Select(tool => tool.Name));
        throw new InvalidOperationException($"MCP server did not return a tool named '{toolName}'. Available tools: {availableTools}.");
    }

    private static JsonObject GetMcpToolArguments(InteractionInputCollection arguments)
    {
        var value = GetMcpArgumentValue(arguments, McpToolArgumentsArgumentName);
        return string.IsNullOrWhiteSpace(value)
            ? []
            : TryParseJsonObject(value, out var result)
                ? result
                : throw new InvalidOperationException("MCP tool arguments must be a valid JSON object.");
    }

    private static async Task<(McpTool Tool, JsonObject Arguments)> GetMcpToolCallAsync(McpToolCommandContext ctx, IReadOnlyList<McpTool> tools)
    {
        var interactionService = ctx.Services.GetRequiredService<IInteractionService>();
        if (!string.IsNullOrWhiteSpace(GetMcpArgumentValue(ctx.Arguments, McpToolArgumentName)) || !interactionService.IsAvailable)
        {
            var selectedTool = GetSelectedMcpTool(ctx.Arguments, tools);
            return (selectedTool, GetMcpToolArguments(ctx.Arguments));
        }

        var promptedTool = await PromptForMcpToolAsync(ctx, tools).ConfigureAwait(true);
        return (promptedTool, await PromptForMcpToolArgumentsAsync(ctx, promptedTool).ConfigureAwait(true));
    }

    private static string? GetMcpArgumentValue(InteractionInputCollection arguments, string name)
    {
        return arguments.TryGetByName(name, out var input) ? input.Value : null;
    }

    private static async Task<McpTool> PromptForMcpToolAsync(McpToolCommandContext ctx, IReadOnlyList<McpTool> tools)
    {
        var interactionService = ctx.Services.GetRequiredService<IInteractionService>();
        var toolInput = new InteractionInput
        {
            Name = McpToolArgumentName,
            Label = "Tool",
            InputType = InputType.Choice,
            Required = true,
            Options = tools.Select(tool => new KeyValuePair<string, string>(tool.Name, string.IsNullOrWhiteSpace(tool.Description) ? tool.Name : $"{tool.Name} - {tool.Description}")).ToArray()
        };

        var result = await interactionService.PromptInputAsync(
            title: "MCP Tool",
            message: "Choose the MCP tool to invoke.",
            input: toolInput,
            cancellationToken: ctx.CancellationToken).ConfigureAwait(true);

        if (result.Canceled || string.IsNullOrWhiteSpace(result.Data.Value))
        {
            throw new McpDialogCanceledException("User canceled the MCP tool prompt.");
        }

        return tools.First(tool => string.Equals(tool.Name, result.Data.Value, StringComparison.Ordinal));
    }

    private static async Task<JsonObject> PromptForMcpToolArgumentsAsync(McpToolCommandContext ctx, McpTool tool)
    {
        var parameterInputs = CreateMcpToolParameterInputs(tool);
        if (parameterInputs.Count > 0)
        {
            return await PromptForMcpToolParameterInputsAsync(ctx, tool, parameterInputs).ConfigureAwait(true);
        }

        if (tool.InputSchema?["properties"] is JsonObject { Count: 0 })
        {
            return [];
        }

        return await PromptForMcpToolRawArgumentsAsync(ctx, tool).ConfigureAwait(true);
    }

    private static async Task<JsonObject> PromptForMcpToolParameterInputsAsync(McpToolCommandContext ctx, McpTool tool, IReadOnlyList<McpToolParameterInput> parameterInputs)
    {
        var interactionService = ctx.Services.GetRequiredService<IInteractionService>();
        var result = await interactionService.PromptInputsAsync(
            title: $"Invoke {tool.Name}",
            message: "Enter the tool arguments.",
            inputs: parameterInputs.Select(p => p.Input).ToArray(),
            options: new InputsDialogInteractionOptions
            {
                ValidationCallback = context =>
                {
                    foreach (var parameterInput in parameterInputs)
                    {
                        var input = context.Inputs[parameterInput.Input.Name];
                        if (string.IsNullOrWhiteSpace(input.Value) && !parameterInput.Input.Required)
                        {
                            continue;
                        }

                        if (!TryCreateMcpToolArgumentValue(parameterInput.Parameter, input.Value, out _, out var errorMessage))
                        {
                            context.AddValidationError(input, errorMessage);
                        }
                    }

                    return Task.CompletedTask;
                }
            },
            cancellationToken: ctx.CancellationToken).ConfigureAwait(true);

        if (result.Canceled)
        {
            throw new McpDialogCanceledException("User canceled the MCP arguments prompt.");
        }

        var arguments = new JsonObject();
        foreach (var parameterInput in parameterInputs)
        {
            var input = result.Data[parameterInput.Input.Name];
            if (string.IsNullOrWhiteSpace(input.Value) && !parameterInput.Input.Required)
            {
                continue;
            }

            if (!TryCreateMcpToolArgumentValue(parameterInput.Parameter, input.Value, out var value, out var errorMessage))
            {
                throw new InvalidOperationException(errorMessage);
            }

            arguments[parameterInput.Parameter.Name] = value;
        }

        return arguments;
    }

    private static async Task<JsonObject> PromptForMcpToolRawArgumentsAsync(McpToolCommandContext ctx, McpTool tool)
    {
        var interactionService = ctx.Services.GetRequiredService<IInteractionService>();
        var argumentsInput = new InteractionInput
        {
            Name = McpToolArgumentsArgumentName,
            Label = "Arguments JSON",
            InputType = InputType.Text,
            Required = true,
            Value = CreateMcpToolArgumentsTemplate(tool).ToString(),
            Placeholder = "{ \"question\": \"What can you help with?\" }",
            Description = CreateMcpToolArgumentsDescription(tool),
            EnableDescriptionMarkdown = true
        };

        var result = await interactionService.PromptInputAsync(
            title: $"Invoke {tool.Name}",
            message: "Enter the JSON object to pass as the tool arguments.",
            input: argumentsInput,
            options: new InputsDialogInteractionOptions
            {
                ValidationCallback = context =>
                {
                    var arguments = context.Inputs[McpToolArgumentsArgumentName];
                    if (!TryParseJsonObject(arguments.Value, out _))
                    {
                        context.AddValidationError(arguments, "Arguments must be a valid JSON object.");
                    }

                    return Task.CompletedTask;
                }
            },
            cancellationToken: ctx.CancellationToken).ConfigureAwait(true);

        if (result.Canceled || string.IsNullOrWhiteSpace(result.Data.Value))
        {
            throw new McpDialogCanceledException("User canceled the MCP arguments prompt.");
        }

        return TryParseJsonObject(result.Data.Value, out var arguments)
            ? arguments
            : throw new InvalidOperationException("Arguments must be a valid JSON object.");
    }

    private static IReadOnlyList<McpToolParameterInput> CreateMcpToolParameterInputs(McpTool tool)
    {
        if (tool.InputSchema?["properties"] is not JsonObject properties || properties.Count == 0)
        {
            return [];
        }

        var requiredParameters = ReadMcpRequiredParameters(tool.InputSchema);
        var inputs = new List<McpToolParameterInput>();
        foreach (var property in properties)
        {
            if (property.Value is not JsonObject propertySchema)
            {
                return [];
            }

            var parameter = new McpToolParameter(
                property.Key,
                GetMcpJsonSchemaType(propertySchema),
                propertySchema,
                requiredParameters.Contains(property.Key));

            if (parameter.Type is not ("string" or "boolean" or "integer" or "number" or "array" or "object") ||
                propertySchema.ContainsKey("anyOf") || propertySchema.ContainsKey("oneOf") ||
                propertySchema.ContainsKey("allOf") || propertySchema.ContainsKey("$ref"))
            {
                return [];
            }

            inputs.Add(new McpToolParameterInput(parameter, CreateMcpToolParameterInput(parameter)));
        }

        return inputs;
    }

    private static InteractionInput CreateMcpToolParameterInput(McpToolParameter parameter)
    {
        var schema = parameter.Schema;
        var inputType = parameter.Type switch
        {
            "boolean" => InputType.Boolean,
            "integer" or "number" => InputType.Number,
            _ when schema["enum"] is JsonArray enumValues && enumValues.All(value => value is not null) => InputType.Choice,
            _ => InputType.Text
        };

        var input = new InteractionInput
        {
            Name = parameter.Name,
            Label = parameter.Name,
            InputType = inputType,
            Required = parameter.Required,
            Value = GetMcpToolParameterDefaultValue(parameter),
            Placeholder = GetMcpToolParameterPlaceholder(parameter),
            Description = schema["description"]?.GetValue<string>(),
        };

        if (inputType == InputType.Choice && schema["enum"] is JsonArray choices)
        {
            input.Options = choices
                .Where(value => value is not null)
                .Select(value =>
                {
                    var option = GetMcpJsonValueAsString(value!);
                    return new KeyValuePair<string, string>(option, option);
                })
                .ToArray();
        }

        return input;
    }

    private static string? GetMcpToolParameterDefaultValue(McpToolParameter parameter)
    {
        if (parameter.Schema["default"] is { } defaultValue)
        {
            return defaultValue switch
            {
                JsonValue value when value.TryGetValue<string>(out var stringValue) => stringValue,
                _ => defaultValue.ToJsonString()
            };
        }

        return null;
    }

    private static string? GetMcpToolParameterPlaceholder(McpToolParameter parameter)
    {
        return parameter.Type switch
        {
            "boolean" => "true",
            "integer" => "42",
            "number" => "42.0",
            "array" => "[\"value\"]",
            "object" => "{ \"key\": \"value\" }",
            _ => "value"
        };
    }

    private static bool TryCreateMcpToolArgumentValue(McpToolParameter parameter, string? value, [NotNullWhen(true)] out JsonNode? result, [NotNullWhen(false)] out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            if (parameter.Required)
            {
                errorMessage = $"{parameter.Name} is required.";
                return false;
            }

            result = JsonValue.Create(string.Empty);
            return true;
        }

        switch (parameter.Type)
        {
            case "boolean":
                if (bool.TryParse(value, out var boolValue))
                {
                    result = JsonValue.Create(boolValue);
                    return true;
                }

                errorMessage = $"{parameter.Name} must be true or false.";
                return false;

            case "integer":
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                {
                    result = JsonValue.Create(longValue);
                    return true;
                }

                errorMessage = $"{parameter.Name} must be an integer.";
                return false;

            case "number":
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                {
                    result = JsonValue.Create(doubleValue);
                    return true;
                }

                errorMessage = $"{parameter.Name} must be a number.";
                return false;

            case "array":
                return TryParseJsonNode(value, JsonValueKind.Array, parameter.Name, out result, out errorMessage);

            case "object":
                return TryParseJsonNode(value, JsonValueKind.Object, parameter.Name, out result, out errorMessage);

            default:
                result = JsonValue.Create(value);
                return true;
        }
    }

    private static bool TryParseJsonNode(string value, JsonValueKind expectedKind, string parameterName, [NotNullWhen(true)] out JsonNode? result, [NotNullWhen(false)] out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        try
        {
            result = JsonNode.Parse(value);
            if (result is null || result.GetValueKind() != expectedKind)
            {
                errorMessage = $"{parameterName} must be a JSON {expectedKind.ToString().ToLowerInvariant()}.";
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            errorMessage = $"{parameterName} must be valid JSON.";
            return false;
        }
    }

    private static HashSet<string> ReadMcpRequiredParameters(JsonObject schema)
    {
        var requiredParameters = new HashSet<string>(StringComparer.Ordinal);
        if (schema["required"] is not JsonArray required)
        {
            return requiredParameters;
        }

        foreach (var requiredParameter in required)
        {
            if (requiredParameter?.GetValue<string>() is { } name)
            {
                requiredParameters.Add(name);
            }
        }

        return requiredParameters;
    }

    private static string? GetMcpJsonSchemaType(JsonObject schema)
    {
        if (schema["type"] is JsonValue typeValue && typeValue.TryGetValue<string>(out var type))
        {
            return type;
        }

        return null;
    }

    private static string GetMcpJsonValueAsString(JsonNode value)
    {
        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var stringValue) => stringValue,
            _ => value.ToJsonString()
        };
    }

    private static JsonObject CreateMcpToolArgumentsTemplate(McpTool tool)
    {
        var result = new JsonObject();
        if (tool.InputSchema?["properties"] is not JsonObject properties)
        {
            return result;
        }

        var requiredParameters = ReadMcpRequiredParameters(tool.InputSchema);
        foreach (var property in properties)
        {
            var propertySchema = property.Value as JsonObject;
            if (propertySchema?["default"] is { } defaultValue)
            {
                result[property.Key] = defaultValue.DeepClone();
            }
            else if (requiredParameters.Contains(property.Key))
            {
                result[property.Key] = GetMcpDefaultArgumentValue(propertySchema);
            }
        }

        return result;
    }

    private static JsonNode? GetMcpDefaultArgumentValue(JsonObject? propertySchema)
    {
        var type = propertySchema is null ? null : GetMcpJsonSchemaType(propertySchema);
        return type switch
        {
            "boolean" => false,
            "integer" or "number" => 0,
            "array" => new JsonArray(),
            "object" => new JsonObject(),
            _ => ""
        };
    }

    private static string CreateMcpToolArgumentsDescription(McpTool tool)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(tool.Description))
        {
            builder.AppendLine(tool.Description);
            builder.AppendLine();
        }

        if (tool.InputSchema is not null)
        {
            builder.AppendLine("Input schema:");
            builder.AppendLine("```json");
            builder.AppendLine(JsonSerializer.Serialize(tool.InputSchema, s_indentedJsonOptions));
            builder.AppendLine("```");
        }

        return builder.ToString();
    }

    private static bool TryParseJsonObject(string? value, [NotNullWhen(true)] out JsonObject? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            result = JsonNode.Parse(value) as JsonObject;
            return result is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed class McpCleanupHandler(HttpClient httpClient, ILogger logger) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // HttpClient cannot send the same message twice. Forward through the configured
            // AppHost client without bypassing its default headers or message handlers.
            using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content,
                Version = request.Version,
                VersionPolicy = request.VersionPolicy
            };
            foreach (var header in request.Headers)
            {
                forwarded.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (request.Method != HttpMethod.Delete)
            {
                return await httpClient.SendAsync(forwarded, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }

            // The SDK owns DELETE, but does not bound it or report unsuccessful status codes.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var response = await httpClient.SendAsync(forwarded, HttpCompletionOption.ResponseHeadersRead, cleanup.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.MethodNotAllowed)
                {
                    logger.LogWarning("Could not terminate the MCP session. HTTP status: {StatusCode}.", (int)response.StatusCode);
                }

                return response;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                // Do not log request URLs, headers, bodies, or server-controlled exception messages.
                logger.LogWarning("Could not terminate the MCP session. Failure type: {FailureType}.", ex.GetType().Name);
                throw;
            }
        }
    }

    private sealed record McpToolCommandContext(
        IServiceProvider Services,
        CancellationToken CancellationToken,
        InteractionInputCollection Arguments);

    private sealed class McpDialogCanceledException(string message) : OperationCanceledException(message);

    private sealed record McpTool(string Name, string? Description, JsonObject? InputSchema);

    private sealed record McpToolParameter(string Name, string? Type, JsonObject Schema, bool Required);

    private sealed record McpToolParameterInput(McpToolParameter Parameter, InteractionInput Input);

    private static readonly JsonSerializerOptions s_indentedJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}

#pragma warning restore ASPIREINTERACTION001
