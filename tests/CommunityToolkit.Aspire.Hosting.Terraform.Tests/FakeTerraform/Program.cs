// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

var trace = Environment.GetEnvironmentVariable("TERRAFORM_TEST_TRACE")
    ?? throw new InvalidOperationException("TERRAFORM_TEST_TRACE is required.");
File.AppendAllText(trace, JsonSerializer.Serialize(new
{
    Arguments = args,
    WorkingDirectory = Directory.GetCurrentDirectory(),
    Input = Environment.GetEnvironmentVariable("TF_INPUT"),
    Automation = Environment.GetEnvironmentVariable("TF_IN_AUTOMATION"),
    Message = Environment.GetEnvironmentVariable("TF_VAR_message"),
    PlanOptions = Environment.GetEnvironmentVariable("TF_CLI_ARGS_plan"),
    ApplyOptions = Environment.GetEnvironmentVariable("TF_CLI_ARGS_apply")
}) + Environment.NewLine);

if (Environment.GetEnvironmentVariable("TERRAFORM_TEST_FAIL") == "true")
{
    Console.Error.WriteLine($"Error: {Environment.GetEnvironmentVariable("TERRAFORM_TEST_SECRET")}");
    return 17;
}

if (args[0] == "plan")
{
    var output = args.Single(arg => arg.StartsWith("-out=", StringComparison.Ordinal))["-out=".Length..];
    File.WriteAllText(output, "fake saved plan");
    return 2;
}

if (args[0] == "apply" && !File.Exists(args[^1]))
{
    Console.Error.WriteLine("Saved plan is missing.");
    return 3;
}

return 0;