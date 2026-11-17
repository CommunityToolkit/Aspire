// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.Terraform.Tests;

public class TerraformPipelineExecutionTests
{
    [Fact]
    public async Task PipelineRunsDeclaredCommandsAndAppliesTheSavedPlan()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var appHost = Path.Combine(root, "examples", "terraform", "CommunityToolkit.Aspire.Hosting.Terraform.AppHost", "CommunityToolkit.Aspire.Hosting.Terraform.AppHost.csproj");
        var executable = Path.Combine(AppContext.BaseDirectory, "fake-terraform", OperatingSystem.IsWindows() ? "FakeTerraform.exe" : "FakeTerraform");
        var directory = Directory.CreateTempSubdirectory("aspire-terraform-test-");
        var trace = Path.Combine(directory.FullName, "trace.jsonl");
        try
        {
            await RunStep("init");
            await RunStep("plan");
            var plan = Path.Combine(directory.FullName, "sample.tfplan");
            Assert.True(File.Exists(plan));
            await RunStep("apply");
            await RunStep("destroy");

            var lines = await File.ReadAllLinesAsync(trace, TestContext.Current.CancellationToken);
            Assert.Equal(4, lines.Length);
            var commands = lines.Select(line => JsonDocument.Parse(line)).ToArray();
            try
            {
                Assert.Equal(["init", "plan", "apply", "destroy"],
                    commands.Select(item => item.RootElement.GetProperty("Arguments")[0].GetString()));
                Assert.All(commands, item =>
                {
                    var json = item.RootElement;
                    Assert.Equal(directory.FullName, json.GetProperty("WorkingDirectory").GetString());
                    Assert.Equal("false", json.GetProperty("Input").GetString());
                    Assert.Equal("true", json.GetProperty("Automation").GetString());
                    Assert.Equal("Hello from Aspire", json.GetProperty("Message").GetString());
                    Assert.Contains("-no-color", json.GetProperty("Arguments").EnumerateArray().Select(arg => arg.GetString()));
                });
                Assert.Contains("-detailed-exitcode", commands[1].RootElement.GetProperty("Arguments").EnumerateArray().Select(arg => arg.GetString()));
                Assert.Contains(
                    $"'-var-file={Path.Combine(directory.FullName, "variables with spaces.tfvars")}'",
                    commands[1].RootElement.GetProperty("PlanOptions").GetString());
                Assert.Equal("-lock-timeout=60s", commands[2].RootElement.GetProperty("ApplyOptions").GetString());
                Assert.Equal(plan, commands[2].RootElement.GetProperty("Arguments").EnumerateArray().Last().GetString());
            }
            finally
            {
                foreach (var command in commands)
                {
                    command.Dispose();
                }
            }

            File.Delete(plan);
            var failed = await RunStep("apply", expectSuccess: false);
            Assert.Contains("Saved Terraform plan", failed, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(4, File.ReadAllLines(trace).Length);

            var failure = await RunStep("init", expectSuccess: false, failTerraform: true);
            Assert.Contains("exit code 17", failure, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("[REDACTED]", failure);
            Assert.DoesNotContain("sensitive-test-value", failure);
        }
        finally
        {
            directory.Delete(recursive: true);
        }

        async Task<string> RunStep(string operation, bool expectSuccess = true, bool failTerraform = false)
        {
            var info = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "aspire")
            {
                WorkingDirectory = Path.GetDirectoryName(appHost)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            if (OperatingSystem.IsWindows())
            {
                info.ArgumentList.Add("/d");
                info.ArgumentList.Add("/c");
                info.ArgumentList.Add("aspire");
            }
            foreach (var argument in new[] { "do", $"terraform-{operation}-sample", "--apphost", appHost, "--no-build", "--non-interactive", "--nologo" })
            {
                info.ArgumentList.Add(argument);
            }
            info.Environment["TerraformDirectory"] = directory.FullName;
            info.Environment["TerraformExecutable"] = executable;
            info.Environment["TerraformVarsFile"] = Path.Combine(directory.FullName, "variables with spaces.tfvars");
            info.Environment["TERRAFORM_TEST_TRACE"] = trace;
            if (failTerraform)
            {
                info.Environment["TERRAFORM_TEST_FAIL"] = "true";
                info.Environment["TERRAFORM_TEST_SECRET"] = "sensitive-test-value";
            }
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Failed to start Aspire CLI.");
            var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            try
            {
                await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                throw;
            }
            var output = await stdout + await stderr;
            Assert.True(expectSuccess ? process.ExitCode == 0 : process.ExitCode != 0, output);
            return output;
        }
    }
}