using System.Text.Json;
using System.Text.Json.Nodes;
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using JKToolKit.CodexSDK.Models;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerCollaborationMode;

public sealed class AppServerCollaborationModeCommand : AsyncCommand<AppServerCollaborationModeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppServerCollaborationModeSettings settings, CancellationToken cancellationToken)
    {
        var repoPath = AppServerThreadCommandHelpers.ResolveRepoPath(settings);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (settings.TimeoutSeconds is > 0)
        {
            cts.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds.Value));
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var ct = cts.Token;

        try
        {
            await ValidateGuardAsync(repoPath, settings, ct);
            await ValidateExperimentalE2EAsync(repoPath, settings, ct);
            Console.WriteLine("ok");
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task ValidateGuardAsync(string repoPath, AppServerCollaborationModeSettings settings, CancellationToken ct)
    {
        Console.WriteLine("Phase 1: stable-only guard (expected failure)...");

        var collab = BuildCollaborationModeJson(mode: "plan", model: CodexModel.Gpt52Codex.Value, effort: null, developerInstructions: null);

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            builder.ConfigureAppServer(o =>
            {
                o.DefaultClientInfo = new("ncodexsdk-demo", "JKToolKit.CodexSDK CollaborationMode Guard Demo", "1.0.0");
                o.ExperimentalApi = false;
            });
        });

        await using var codex = await sdk.AppServer.StartAsync(ct);
        var thread = await codex.StartThreadAsync(new ThreadStartOptions
        {
            Model = CodexModel.Gpt52Codex,
            Cwd = repoPath,
            ApprovalPolicy = CodexApprovalPolicy.Never,
            Sandbox = CodexSandboxMode.WorkspaceWrite
        }, ct);

        try
        {
            await using var _ = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
            {
                Input = [TurnInputItem.Text("This should throw before sending.")],
                CollaborationMode = collab
            }, ct);

            throw new InvalidOperationException("Expected CodexExperimentalApiRequiredException for turn/start.collaborationMode.");
        }
        catch (CodexExperimentalApiRequiredException ex) when (ex.Descriptor == "turn/start.collaborationMode")
        {
            Console.WriteLine($"[expected] {ex.GetType().Name} Descriptor='{ex.Descriptor}'");
        }
    }

    private static async Task ValidateExperimentalE2EAsync(string repoPath, AppServerCollaborationModeSettings settings, CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("Phase 2: experimental API (collaborationMode/list + turn/start.collaborationMode)...");

        await using var sdk = CodexSdk.Create(builder =>
        {
            builder.CodexExecutablePath = settings.CodexExecutablePath;
            builder.CodexHomeDirectory = settings.CodexHomeDirectory;
            builder.ConfigureAppServer(o =>
            {
                o.DefaultClientInfo = new("ncodexsdk-demo", "JKToolKit.CodexSDK CollaborationMode E2E Demo", "1.0.0");
                o.ExperimentalApi = true;
            });
        });

        await using var codex = await sdk.AppServer.StartAsync(ct);

        var list = await codex.CallAsync("collaborationMode/list", new { }, ct);
        if (!TryGetFirstMask(list, out var mode, out var model, out var effort, out var developerInstructions))
        {
            throw new InvalidOperationException($"Unexpected collaborationMode/list result shape: {list}");
        }

        Console.WriteLine($"Preset: mode={mode}, model={model}, effort={effort ?? "n/a"}");

        var collab = BuildCollaborationModeJson(mode, model, effort, developerInstructions);

        var thread = await codex.StartThreadAsync(new ThreadStartOptions
        {
            Model = CodexModel.Gpt52Codex,
            Cwd = repoPath,
            ApprovalPolicy = CodexApprovalPolicy.Never,
            Sandbox = CodexSandboxMode.WorkspaceWrite
        }, ct);

        Console.WriteLine($"Thread: {thread.Id}");
        Console.WriteLine($"> {settings.Prompt}");
        Console.WriteLine();

        await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
        {
            Input = [TurnInputItem.Text(settings.Prompt)],
            CollaborationMode = collab
        }, ct);

        await foreach (var ev in turn.Events(ct))
        {
            if (ev is AgentMessageDeltaNotification delta)
            {
                Console.Write(delta.Delta);
            }
        }

        var completed = await turn.Completion;
        Console.WriteLine($"\nDone: {completed.Status}");
    }

    private static JsonElement BuildCollaborationModeJson(string mode, string model, string? effort, string? developerInstructions)
    {
        var settings = new JsonObject
        {
            ["model"] = model
        };

        if (!string.IsNullOrWhiteSpace(effort))
        {
            settings["reasoning_effort"] = effort;
        }

        if (developerInstructions is not null)
        {
            settings["developer_instructions"] = developerInstructions;
        }

        var collab = new JsonObject
        {
            ["mode"] = mode,
            ["settings"] = settings
        };

        return JsonDocument.Parse(collab.ToJsonString()).RootElement.Clone();
    }

    private static bool TryGetFirstMask(
        JsonElement result,
        out string mode,
        out string model,
        out string? effort,
        out string? developerInstructions)
    {
        mode = "default";
        model = CodexModel.Gpt52Codex.Value;
        effort = null;
        developerInstructions = null;

        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var e = data.EnumerateArray();
        if (!e.MoveNext() || e.Current.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var first = e.Current;

        mode = TryGetString(first, "mode") ?? mode;
        model = TryGetString(first, "model") ?? model;
        effort = TryGetString(first, "reasoning_effort") ?? TryGetString(first, "reasoningEffort");
        developerInstructions = TryGetString(first, "developer_instructions") ?? TryGetString(first, "developerInstructions");
        return true;
    }

    private static string? TryGetString(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!obj.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return prop.GetString();
    }
}
