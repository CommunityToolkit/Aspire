// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Channels;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Testing;

#pragma warning disable ASPIREINTERACTION001 // Type is for evaluation purposes only and is subject to change or removal in future updates.
#pragma warning disable ASPIRETERMINAL001

/// <summary>Identifies the interaction captured by <see cref="TestInteractionService"/>.</summary>
public enum InteractionType
{
    /// <summary>A single input prompt.</summary>
    Input,
    /// <summary>A multiple-input prompt.</summary>
    Inputs,
    /// <summary>A message box.</summary>
    MessageBox,
    /// <summary>A notification.</summary>
    Notification,
    /// <summary>A progress dialog.</summary>
    Progress
}

/// <summary>A captured interaction whose result can be supplied by a test.</summary>
/// <param name="Type">The interaction kind.</param>
/// <param name="Title">The dialog title.</param>
/// <param name="Message">The dialog message.</param>
/// <param name="Inputs">The requested inputs.</param>
/// <param name="Options">The interaction options.</param>
/// <param name="CancellationToken">The caller's cancellation token.</param>
/// <param name="CompletionTcs">Completes the interaction with its typed result.</param>
public sealed record InteractionData(InteractionType Type, string Title, string? Message, InteractionInputCollection Inputs, InteractionOptions? Options, CancellationToken CancellationToken, TaskCompletionSource<object> CompletionTcs);

/// <summary>Captures dashboard interactions and lets tests supply responses without a dashboard.</summary>
public sealed class TestInteractionService : IInteractionService
{
    /// <inheritdoc />
    public Task<InteractionResult<bool>> PromptTerminalAsync(string message, AspireTerminal terminal, TerminalInteractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>Gets the channel of pending interactions.</summary>
    public Channel<InteractionData> Interactions { get; } = Channel.CreateUnbounded<InteractionData>();

    /// <inheritdoc />
    public bool IsAvailable { get; set; } = true;

    /// <inheritdoc />
    public Task<InteractionResult<bool>> PromptConfirmationAsync(string title, string message, MessageBoxInteractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<InteractionResult<InteractionInput>> PromptInputAsync(string title, string? message, string inputLabel, string placeHolder, InputsDialogInteractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<InteractionResult<InteractionInput>> PromptInputAsync(string title, string? message, InteractionInput input, InputsDialogInteractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var data = new InteractionData(InteractionType.Input, title, message, new InteractionInputCollection([input]), options, cancellationToken, new TaskCompletionSource<object>());
        Interactions.Writer.TryWrite(data);
        var result = (InteractionResult<InteractionInput>)await data.CompletionTcs.Task;
        return result;
    }

    /// <inheritdoc />
    public async Task<InteractionResult<InteractionInputCollection>> PromptInputsAsync(string title, string? message, IReadOnlyList<InteractionInput> inputs, InputsDialogInteractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var data = new InteractionData(InteractionType.Inputs, title, message, new InteractionInputCollection(inputs), options, cancellationToken, new TaskCompletionSource<object>());
        Interactions.Writer.TryWrite(data);
        var result = (InteractionResult<InteractionInputCollection>)await data.CompletionTcs.Task;

        // Convert the result to use InteractionInputCollection
        if (result.Canceled)
        {
            return InteractionResult.Cancel<InteractionInputCollection>();
        }

        return InteractionResult.Ok(new InteractionInputCollection(result.Data));
    }

    /// <inheritdoc />
    public async Task<InteractionResult<bool>> PromptNotificationAsync(string title, string message, NotificationInteractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var data = new InteractionData(InteractionType.Notification, title, message, new InteractionInputCollection([]), options, cancellationToken, new TaskCompletionSource<object>());
        Interactions.Writer.TryWrite(data);
        return (InteractionResult<bool>)await data.CompletionTcs.Task;
    }

    /// <inheritdoc />
    public async Task<InteractionResult<bool>> PromptMessageBoxAsync(string title, string message, MessageBoxInteractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var data = new InteractionData(InteractionType.MessageBox, title, message, new InteractionInputCollection([]), options, cancellationToken, new TaskCompletionSource<object>());
        Interactions.Writer.TryWrite(data);
        return (InteractionResult<bool>)await data.CompletionTcs.Task;
    }

    /// <summary>Gets whether a progress interaction was requested.</summary>
    public bool PromptProgressCalled { get; private set; }

    /// <inheritdoc />
    public async Task<InteractionResult<bool>> PromptProgressAsync(string message, ProgressInteractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        PromptProgressCalled = true;

        if (options?.Work is { } work)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var progressContext = new ProgressContext { CancellationToken = cts.Token };

            var data = new InteractionData(InteractionType.Progress, options.Title ?? string.Empty, message, new InteractionInputCollection([]), options, cancellationToken, new TaskCompletionSource<object>());
            Interactions.Writer.TryWrite(data);

            // Run the work and handle button clicks (CompletionTcs) canceling the work.
            var workTask = work(progressContext);
            var completionTask = data.CompletionTcs.Task;

            var finished = await Task.WhenAny(workTask, completionTask).ConfigureAwait(false);
            if (finished == completionTask)
            {
                // Button was clicked — cancel the work.
                cts.Cancel();
                try
                {
                    await workTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }

                return InteractionResult.Cancel<bool>();
            }

            await workTask.ConfigureAwait(false);
            return InteractionResult.Ok(true);
        }

        return InteractionResult.Ok(true);
    }
}
