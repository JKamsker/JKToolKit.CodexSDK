using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AuthAccountConfigWrappersTests
{
    [Fact]
    public async Task GetConversationSummaryAsync_CallsExpectedMethod_AndParsesResponse()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "getConversationSummary",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("conversationId").GetString().Should().Be("thr-123");
            },
            Result = JsonSerializer.SerializeToElement(new
            {
                summary = new
                {
                    conversationId = "thr-123",
                    path = "C:/codex/home/sessions/2026/04/01/rollout.jsonl",
                    preview = "hello",
                    timestamp = "2026-04-01T10:00:00Z",
                    updatedAt = "2026-04-01T10:01:00Z",
                    modelProvider = "openai",
                    cwd = "C:/repo",
                    cliVersion = "0.118.0",
                    source = "exec",
                    gitInfo = new
                    {
                        sha = "abc123",
                        branch = "main",
                        originUrl = "https://example.test/repo.git"
                    }
                }
            })
        };

        await using var client = CreateClient(rpc);

        var result = await client.GetConversationSummaryAsync(new ConversationSummaryOptions
        {
            ConversationId = "thr-123"
        });

        result.Summary.ConversationId.Should().Be("thr-123");
        result.Summary.Path.Should().Be("C:/codex/home/sessions/2026/04/01/rollout.jsonl");
        result.Summary.GitInfo.Should().NotBeNull();
        result.Summary.GitInfo!.Sha.Should().Be("abc123");
    }

    [Fact]
    public async Task GetConversationSummaryAsync_RequiresExactlyOneSelector()
    {
        await using var client = CreateClient(new FakeRpc());

        var act = async () => await client.GetConversationSummaryAsync(new ConversationSummaryOptions
        {
            ConversationId = "thr-123",
            RolloutPath = "C:/codex/home/rollout.jsonl"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Exactly one of ConversationId or RolloutPath*");
    }

    [Fact]
    public async Task GetGitDiffToRemoteAsync_CallsExpectedMethod_AndParsesResponse()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "gitDiffToRemote",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("cwd").GetString().Should().Be(XPaths.Abs("repo"));
            },
            Result = JsonSerializer.SerializeToElement(new
            {
                sha = "deadbeef",
                diff = "diff --git a/a.txt b/a.txt"
            })
        };

        await using var client = CreateClient(rpc);

        var result = await client.GetGitDiffToRemoteAsync(new GitDiffToRemoteOptions
        {
            Cwd = XPaths.Abs("repo")
        });

        result.Sha.Should().Be("deadbeef");
        result.Diff.Should().Contain("diff --git");
    }

    [Fact]
    public async Task GetAuthStatusAsync_CallsExpectedMethod_AndParsesResponse()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "getAuthStatus",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("includeToken").GetBoolean().Should().BeTrue();
                json.GetProperty("refreshToken").GetBoolean().Should().BeFalse();
            },
            Result = JsonSerializer.SerializeToElement(new
            {
                authMethod = "chatgpt",
                authToken = "secret-token",
                requiresOpenaiAuth = false
            })
        };

        await using var client = CreateClient(rpc);

        var result = await client.GetAuthStatusAsync(new AuthStatusOptions
        {
            IncludeToken = true,
            RefreshToken = false
        });

        result.AuthMethod.Should().Be(CodexAuthMode.ChatGpt);
        result.AuthToken.Should().Be("secret-token");
        result.RequiresOpenaiAuth.Should().BeFalse();
    }

    [Fact]
    public async Task ReadAccountAsync_CallsExpectedMethod_AndParsesResponse()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            account = new
            {
                type = "chatgpt",
                email = "person@example.test",
                planType = "plus"
            },
            requiresOpenaiAuth = false
        });

        var rpc = new FakeRpc
        {
            AssertMethod = "account/read",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("refreshToken").GetBoolean().Should().BeTrue();
            },
            Result = rawResult
        };

        await using var client = CreateClient(rpc);

        var result = await client.ReadAccountAsync(new AccountReadOptions
        {
            RefreshToken = true
        });

        result.RequiresOpenaiAuth.Should().BeFalse();
        result.Account.Should().NotBeNull();
        result.Account!.Value.GetProperty("type").GetString().Should().Be("chatgpt");
        var account = result.AccountInfo.Should().BeOfType<CodexChatGptAccountInfo>().Subject;
        account.Email.Should().Be("person@example.test");
        account.PlanType.Should().Be(CodexPlanType.Plus);
    }

    [Fact]
    public async Task ReadAccountAsync_ParsesApiKeyAndUnknownPlanTypes()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "account/read",
            Result = JsonSerializer.SerializeToElement(new
            {
                account = new
                {
                    type = "apiKey"
                },
                requiresOpenaiAuth = true
            })
        };

        await using var client = CreateClient(rpc);

        var apiKeyAccount = await client.ReadAccountAsync();

        apiKeyAccount.RequiresOpenaiAuth.Should().BeTrue();
        apiKeyAccount.AccountInfo.Should().BeOfType<CodexApiKeyAccountInfo>();

        rpc.Result = JsonSerializer.SerializeToElement(new
        {
            account = new
            {
                type = "chatgpt",
                email = "person@example.test",
                planType = "unknown"
            },
            requiresOpenaiAuth = true
        });

        var unknownPlanAccount = await client.ReadAccountAsync();

        unknownPlanAccount.AccountInfo.Should().BeOfType<CodexChatGptAccountInfo>()
            .Which.PlanType.Should().Be(CodexPlanType.Unknown);
    }

    [Fact]
    public async Task ReadAccountAsync_MissingRequiredBool_Throws()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "account/read",
            Result = JsonSerializer.SerializeToElement(new
            {
                account = new
                {
                    type = "apiKey"
                }
            })
        };

        await using var client = CreateClient(rpc);

        var act = async () => await client.ReadAccountAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requiresOpenaiAuth*");
    }

    [Fact]
    public async Task ReadAccountRateLimitsAsync_CallsExpectedMethod_AndParsesResponse()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            rateLimits = new
            {
                limitId = "codex",
                planType = "pro"
            },
            rateLimitsByLimitId = new
            {
                codex = new { limitId = "codex" },
                secondary = new { limitId = "secondary" }
            }
        });

        var rpc = new FakeRpc
        {
            AssertMethod = "account/rateLimits/read",
            AssertParams = p => p.Should().BeNull(),
            Result = rawResult
        };

        await using var client = CreateClient(rpc);

        var result = await client.ReadAccountRateLimitsAsync();

        result.RateLimits.GetProperty("limitId").GetString().Should().Be("codex");
        result.RateLimits.GetProperty("planType").GetString().Should().Be("pro");
        result.RateLimitsByLimitId.Should().NotBeNull();
        result.RateLimitsByLimitId!.Should().ContainKey("codex");
        result.RateLimitsByLimitId!["secondary"].GetProperty("limitId").GetString().Should().Be("secondary");
    }

    [Fact]
    public async Task ListModelsAsync_CallsExpectedMethod_AndParsesResponse()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            data = new object[]
            {
                new
                {
                    id = "m1",
                    model = "gpt-5.1-codex",
                    displayName = "GPT-5.1 Codex",
                    description = "Code model",
                    hidden = false,
                    isDefault = true,
                    supportsPersonality = true,
                    defaultReasoningEffort = "medium",
                    inputModalities = new[] { "text" },
                    supportedReasoningEfforts = new[]
                    {
                        new { reasoningEffort = "low", description = "Fast" },
                        new { reasoningEffort = "medium", description = "Balanced" }
                    },
                    availabilityNux = new { message = "Available" },
                    upgrade = "gpt-5.2-codex",
                    upgradeInfo = new
                    {
                        model = "gpt-5.2-codex",
                        upgradeCopy = "Upgrade",
                        modelLink = "https://example.test/model",
                        migrationMarkdown = "Use the newer model."
                    }
                }
            },
            nextCursor = "cursor-2"
        });

        var rpc = new FakeRpc
        {
            AssertMethod = "model/list",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("includeHidden").GetBoolean().Should().BeTrue();
                json.GetProperty("limit").GetInt32().Should().Be(25);
                json.GetProperty("cursor").GetString().Should().Be("cursor-1");
            },
            Result = rawResult
        };

        await using var client = CreateClient(rpc);

        var result = await client.ListModelsAsync(new ModelListOptions
        {
            IncludeHidden = true,
            Limit = 25,
            Cursor = "cursor-1"
        });

        result.NextCursor.Should().Be("cursor-2");
        result.Data.Should().ContainSingle();
        result.Data[0].Id.Should().Be("m1");
        result.Data[0].SupportedReasoningEfforts.Should().HaveCount(2);
        result.Data[0].UpgradeInfo!.Model.Should().Be("gpt-5.2-codex");
    }

    [Fact]
    public async Task ListExperimentalFeaturesAsync_DoesNotRequireExperimentalApi()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "experimentalFeature/list",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("limit").ValueKind.Should().Be(JsonValueKind.Null);
                json.GetProperty("cursor").ValueKind.Should().Be(JsonValueKind.Null);
            },
            Result = JsonSerializer.SerializeToElement(new { data = Array.Empty<object>() })
        };

        await using var client = CreateClient(rpc);

        var result = await client.ListExperimentalFeaturesAsync(new ExperimentalFeatureListOptions());

        result.Data.Should().BeEmpty();
        rpc.SendRequestCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ListExperimentalFeaturesAsync_CallsExpectedMethod_AndParsesResponse()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "experimentalFeature/list",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("limit").GetInt32().Should().Be(10);
            },
            Result = JsonSerializer.SerializeToElement(new
            {
                data = new object[]
                {
                    new
                    {
                        name = "apps",
                        stage = "beta",
                        displayName = "Apps",
                        description = "Connectors",
                        announcement = "Try it",
                        enabled = true,
                        defaultEnabled = false
                    }
                },
                nextCursor = "cursor-3"
            })
        };

        await using var client = CreateClient(rpc);

        var result = await client.ListExperimentalFeaturesAsync(new ExperimentalFeatureListOptions { Limit = 10 });

        result.NextCursor.Should().Be("cursor-3");
        result.Data.Should().ContainSingle();
        result.Data[0].Name.Should().Be("apps");
        result.Data[0].Stage.Should().Be("beta");
    }

    [Fact]
    public async Task WriteConfigValueAsync_CallsExpectedMethod()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "config/value/write",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("keyPath").GetString().Should().Be("model");
                json.GetProperty("mergeStrategy").GetString().Should().Be("replace");
                json.GetProperty("value").GetString().Should().Be("gpt-5.1-codex");
                json.GetProperty("expectedVersion").GetString().Should().Be("v1");
            },
            Result = JsonSerializer.SerializeToElement(new
            {
                status = "okOverridden",
                version = "v2",
                filePath = "C:/repo/.codex/config.toml",
                overriddenMetadata = new
                {
                    message = "Overridden by project config",
                    overridingLayer = new
                    {
                        name = new { type = "project", file = "C:/repo/.codex/config.toml" },
                        version = "layer-v1"
                    },
                    effectiveValue = "gpt-5.2-codex"
                }
            })
        };

        await using var client = CreateClient(rpc);

        var result = await client.WriteConfigValueAsync(new ConfigValueWriteOptions
        {
            KeyPath = "model",
            MergeStrategy = ConfigMergeStrategy.Replace,
            Value = JsonSerializer.SerializeToElement("gpt-5.1-codex"),
            ExpectedVersion = "v1"
        });

        result.Status.Should().Be(ConfigWriteStatus.OkOverridden);
        result.Version.Should().Be("v2");
        result.FilePath.Should().Be("C:/repo/.codex/config.toml");
        result.OverriddenMetadata.Should().NotBeNull();
        result.OverriddenMetadata!.Message.Should().Be("Overridden by project config");
    }

    [Fact]
    public async Task WriteConfigBatchAsync_CallsExpectedMethod()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "config/batchWrite",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("reloadUserConfig").GetBoolean().Should().BeTrue();
                json.GetProperty("edits").GetArrayLength().Should().Be(2);
                json.GetProperty("edits")[1].GetProperty("mergeStrategy").GetString().Should().Be("replace");
            },
            Result = JsonSerializer.SerializeToElement(new
            {
                status = "ok",
                version = "v3",
                filePath = "C:/repo/.codex/config.toml"
            })
        };

        await using var client = CreateClient(rpc);

        var result = await client.WriteConfigBatchAsync(new ConfigBatchWriteOptions
        {
            ReloadUserConfig = true,
            Edits =
            [
                new ConfigEditOperation
                {
                    KeyPath = "model",
                    MergeStrategy = ConfigMergeStrategy.Upsert,
                    Value = JsonSerializer.SerializeToElement("gpt-5.1-codex")
                },
                new ConfigEditOperation
                {
                    KeyPath = "model_provider",
                    MergeStrategy = ConfigMergeStrategy.Replace,
                    Value = JsonSerializer.SerializeToElement("openai")
                }
            ]
        });

        result.Status.Should().Be(ConfigWriteStatus.Ok);
        result.Version.Should().Be("v3");
        result.FilePath.Should().Be("C:/repo/.codex/config.toml");
    }

    [Fact]
    public async Task LogoutAccountAsync_CallsExpectedMethod()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "account/logout",
            AssertParams = p => p.Should().BeNull(),
            Result = JsonSerializer.SerializeToElement(new { })
        };

        await using var client = CreateClient(rpc);

        _ = await client.LogoutAccountAsync();
    }

    [Fact]
    public async Task UploadFeedbackAsync_CallsExpectedMethod_AndParsesThreadId()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "feedback/upload",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("classification").GetString().Should().Be("bug");
                json.GetProperty("includeLogs").GetBoolean().Should().BeTrue();
                json.GetProperty("extraLogFiles").GetArrayLength().Should().Be(1);
            },
            Result = JsonSerializer.SerializeToElement(new { threadId = "feedback-thread-1" })
        };

        await using var client = CreateClient(rpc);

        var result = await client.UploadFeedbackAsync(new FeedbackUploadOptions
        {
            Classification = "bug",
            IncludeLogs = true,
            ExtraLogFiles = ["C:/logs/app.log"]
        });

        result.ThreadId.Should().Be("feedback-thread-1");
    }

    [Fact]
    public async Task StartWindowsSandboxSetupAsync_TypedModeAndCwd_CallsExpectedMethod()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "windowsSandbox/setupStart",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("mode").GetString().Should().Be("elevated");
                json.GetProperty("cwd").GetString().Should().Be(XPaths.Abs("repo"));
            },
            Result = JsonSerializer.SerializeToElement(new { started = true })
        };

        await using var client = CreateClient(rpc);

        var started = await client.StartWindowsSandboxSetupAsync(WindowsSandboxSetupMode.Elevated, cwd: XPaths.Abs("repo"));

        started.Should().BeTrue();
    }

    [Fact]
    public async Task StartWindowsSandboxSetupAsync_StringOverload_RemainsCompatible()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "windowsSandbox/setupStart",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("mode").GetString().Should().Be("unelevated");
            },
            Result = JsonSerializer.SerializeToElement(new { started = true })
        };

        await using var client = CreateClient(rpc);

        var started = await client.StartWindowsSandboxSetupAsync("unelevated");

        started.Should().BeTrue();
    }

    [Fact]
    public async Task StartWindowsSandboxSetupAsync_RelativeCwd_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc
        {
            Result = JsonSerializer.SerializeToElement(new { started = true })
        };

        await using var client = CreateClient(rpc);

        var act = async () => await client.StartWindowsSandboxSetupAsync(
            new WindowsSandboxSetupStartOptions(WindowsSandboxSetupMode.Elevated)
            {
                Cwd = "relative\\repo"
            });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*absolute path*");
        rpc.SendRequestCallCount.Should().Be(0);
    }

    [Fact]
    public async Task WriteSkillsConfigAsync_NameSelector_CallsExpectedMethod()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "skills/config/write",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("enabled").GetBoolean().Should().BeTrue();
                json.GetProperty("name").GetString().Should().Be("my-skill");
                json.TryGetProperty("path", out _).Should().BeFalse();
            },
            Result = JsonSerializer.SerializeToElement(new { effectiveEnabled = true })
        };

        await using var client = CreateClient(rpc);

        var result = await client.WriteSkillsConfigAsync(new SkillsConfigWriteOptions
        {
            Enabled = true,
            Name = "my-skill"
        });

        result.EffectiveEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task WriteSkillsConfigAsync_WhenBothSelectorsProvided_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc
        {
            Result = JsonSerializer.SerializeToElement(new { effectiveEnabled = true })
        };

        await using var client = CreateClient(rpc);

        var act = async () => await client.WriteSkillsConfigAsync(new SkillsConfigWriteOptions
        {
            Enabled = true,
            Name = "my-skill",
            Path = "skills/my-skill"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Exactly one of Path or Name*");
        rpc.SendRequestCallCount.Should().Be(0);
    }

    [Fact]
    public async Task WriteSkillsConfigAsync_PathSelector_RequiresAbsolutePath()
    {
        var rpc = new FakeRpc
        {
            Result = JsonSerializer.SerializeToElement(new { effectiveEnabled = true })
        };

        await using var client = CreateClient(rpc);

        var act = async () => await client.WriteSkillsConfigAsync(new SkillsConfigWriteOptions
        {
            Enabled = true,
            Path = "skills\\my-skill"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*absolute path*");
        rpc.SendRequestCallCount.Should().Be(0);
    }

    [Fact]
    public async Task StartAccountLoginAsync_ChatGptAuthTokens_ThrowsWhenExperimentalApiDisabled()
    {
        var rpc = new FakeRpc
        {
            Result = JsonSerializer.SerializeToElement(new { type = "chatgptAuthTokens" })
        };

        await using var client = CreateClient(rpc);

        var act = async () => await client.StartAccountLoginAsync(new AccountLoginStartOptions.ChatGptAuthTokens("token", "acct_123"));

        var ex = await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        ex.Which.Descriptor.Should().Be("account/login/start.chatgptAuthTokens");
        rpc.SendRequestCallCount.Should().Be(0);
    }

    [Fact]
    public async Task StartAccountLoginAsync_ChatGptAuthTokens_WorksWhenExperimentalApiEnabled()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "account/login/start",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("type").GetString().Should().Be("chatgptAuthTokens");
                json.GetProperty("accessToken").GetString().Should().Be("token");
                json.GetProperty("chatgptAccountId").GetString().Should().Be("acct_123");
                json.GetProperty("chatgptPlanType").GetString().Should().Be("plus");
            },
            Result = JsonSerializer.SerializeToElement(new { type = "chatgptAuthTokens" })
        };

        await using var client = CreateClient(rpc, new CodexAppServerClientOptions
        {
            ExperimentalApi = true
        });

        var result = await client.StartAccountLoginAsync(new AccountLoginStartOptions.ChatGptAuthTokens("token", "acct_123", "plus"));

        result.Should().BeOfType<AccountLoginStartResult.ChatGptAuthTokens>();
    }

    private static CodexAppServerClient CreateClient(FakeRpc rpc, CodexAppServerClientOptions? options = null) =>
        new(
            options ?? new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

    private sealed class FakeProcess : IStdioProcess
    {
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completion => _tcs.Task;

        public int? ProcessId => 1;

        public int? ExitCode => null;

        public IReadOnlyList<string> StderrTail => Array.Empty<string>();

        public ValueTask DisposeAsync()
        {
            _tcs.TrySetCanceled();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeRpc : IJsonRpcConnection
    {
        public string AssertMethod { get; init; } = string.Empty;

        public Action<object?>? AssertParams { get; init; }

        public JsonElement Result { get; set; }

        public int SendRequestCallCount { get; private set; }

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            SendRequestCallCount++;
            if (!string.IsNullOrWhiteSpace(AssertMethod))
            {
                method.Should().Be(AssertMethod);
            }

            AssertParams?.Invoke(@params);
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
