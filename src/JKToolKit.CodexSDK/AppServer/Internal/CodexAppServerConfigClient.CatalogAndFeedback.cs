using System.Text.Json;
using UpstreamV2 = JKToolKit.CodexSDK.Generated.Upstream.AppServer.V2;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerConfigClient
{
    public async Task<ModelListResult> ListModelsAsync(ModelListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "model/list",
            new UpstreamV2.ModelListParams
            {
                Cursor = EmptyToNull(options.Cursor),
                IncludeHidden = options.IncludeHidden,
                Limit = options.Limit
            },
            ct);

        return ParseModelListResult(result);
    }

    public async Task<ExperimentalFeatureListResult> ListExperimentalFeaturesAsync(ExperimentalFeatureListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException("experimentalFeature/list");
        }

        var result = await _sendRequestAsync(
            "experimentalFeature/list",
            new UpstreamV2.ExperimentalFeatureListParams
            {
                Cursor = EmptyToNull(options.Cursor),
                Limit = options.Limit
            },
            ct);

        return ParseExperimentalFeatureListResult(result);
    }

    public async Task<ConfigWriteResult> WriteConfigValueAsync(ConfigValueWriteOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateConfigValueWriteOptions(options);

        var result = await _sendRequestAsync(
            "config/value/write",
            new
            {
                keyPath = options.KeyPath,
                value = options.Value.Clone(),
                mergeStrategy = MapMergeStrategyValue(options.MergeStrategy),
                filePath = EmptyToNull(options.FilePath),
                expectedVersion = EmptyToNull(options.ExpectedVersion)
            },
            ct);

        return new ConfigWriteResult { Raw = result };
    }

    public async Task<ConfigWriteResult> WriteConfigBatchAsync(ConfigBatchWriteOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateConfigBatchWriteOptions(options);

        var result = await _sendRequestAsync(
            "config/batchWrite",
            new
            {
                edits = options.Edits
                    .Select(edit => new
                    {
                        keyPath = edit.KeyPath,
                        value = edit.Value.Clone(),
                        mergeStrategy = MapMergeStrategyValue(edit.MergeStrategy)
                    })
                    .ToArray(),
                filePath = EmptyToNull(options.FilePath),
                expectedVersion = EmptyToNull(options.ExpectedVersion),
                reloadUserConfig = options.ReloadUserConfig
            },
            ct);

        return new ConfigWriteResult { Raw = result };
    }

    public async Task<AccountLogoutResult> LogoutAccountAsync(CancellationToken ct = default)
    {
        var result = await _sendRequestAsync("account/logout", null, ct);
        return new AccountLogoutResult { Raw = result };
    }

    public async Task<FeedbackUploadResult> UploadFeedbackAsync(FeedbackUploadOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Classification))
        {
            throw new ArgumentException("Classification cannot be empty or whitespace.", nameof(options));
        }

        var result = await _sendRequestAsync(
            "feedback/upload",
            new UpstreamV2.FeedbackUploadParams
            {
                Classification = options.Classification,
                Reason = EmptyToNull(options.Reason),
                ThreadId = EmptyToNull(options.ThreadId),
                IncludeLogs = options.IncludeLogs,
                ExtraLogFiles = options.ExtraLogFiles?
                    .Where(static path => !string.IsNullOrWhiteSpace(path))
                    .ToArray()
            },
            ct);

        return new FeedbackUploadResult
        {
            ThreadId = CodexAppServerClientJson.GetRequiredString(result, "threadId", "feedback/upload response"),
            Raw = result
        };
    }

    private static ModelListResult ParseModelListResult(JsonElement result)
    {
        var data = CodexAppServerClientJson.TryGetArray(result, "data");
        if (data is null)
        {
            return new ModelListResult
            {
                Data = Array.Empty<ModelListEntry>(),
                NextCursor = CodexAppServerClientJson.GetStringOrNull(result, "nextCursor"),
                Raw = result
            };
        }

        var entries = new List<ModelListEntry>();
        foreach (var item in data.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = CodexAppServerClientJson.GetStringOrNull(item, "id");
            var model = CodexAppServerClientJson.GetStringOrNull(item, "model");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(model))
            {
                continue;
            }

            entries.Add(new ModelListEntry
            {
                Id = id,
                Model = model,
                DisplayName = CodexAppServerClientJson.GetStringOrNull(item, "displayName") ?? string.Empty,
                Description = CodexAppServerClientJson.GetStringOrNull(item, "description") ?? string.Empty,
                Hidden = CodexAppServerClientJson.GetBoolOrNull(item, "hidden") == true,
                IsDefault = CodexAppServerClientJson.GetBoolOrNull(item, "isDefault") == true,
                SupportsPersonality = CodexAppServerClientJson.GetBoolOrNull(item, "supportsPersonality") == true,
                Upgrade = CodexAppServerClientJson.GetStringOrNull(item, "upgrade"),
                DefaultReasoningEffort = CodexAppServerClientJson.GetStringOrNull(item, "defaultReasoningEffort"),
                AvailabilityNuxMessage = CodexAppServerClientJson.TryGetObject(item, "availabilityNux") is { } availabilityNux
                    ? CodexAppServerClientJson.GetStringOrNull(availabilityNux, "message")
                    : null,
                InputModalities = CodexAppServerClientJson.GetOptionalStringArray(item, "inputModalities") ?? Array.Empty<string>(),
                SupportedReasoningEfforts = ParseReasoningEfforts(item),
                UpgradeInfo = ParseUpgradeInfo(item),
                Raw = item.Clone()
            });
        }

        return new ModelListResult
        {
            Data = entries,
            NextCursor = CodexAppServerClientJson.GetStringOrNull(result, "nextCursor"),
            Raw = result
        };
    }

    private static ExperimentalFeatureListResult ParseExperimentalFeatureListResult(JsonElement result)
    {
        var data = CodexAppServerClientJson.TryGetArray(result, "data");
        if (data is null)
        {
            return new ExperimentalFeatureListResult
            {
                Data = Array.Empty<ExperimentalFeatureListEntry>(),
                NextCursor = CodexAppServerClientJson.GetStringOrNull(result, "nextCursor"),
                Raw = result
            };
        }

        var entries = new List<ExperimentalFeatureListEntry>();
        foreach (var item in data.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = CodexAppServerClientJson.GetStringOrNull(item, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            entries.Add(new ExperimentalFeatureListEntry
            {
                Name = name,
                Stage = CodexAppServerClientJson.GetStringOrNull(item, "stage"),
                DisplayName = CodexAppServerClientJson.GetStringOrNull(item, "displayName"),
                Description = CodexAppServerClientJson.GetStringOrNull(item, "description"),
                Announcement = CodexAppServerClientJson.GetStringOrNull(item, "announcement"),
                Enabled = CodexAppServerClientJson.GetBoolOrNull(item, "enabled") == true,
                DefaultEnabled = CodexAppServerClientJson.GetBoolOrNull(item, "defaultEnabled") == true,
                Raw = item.Clone()
            });
        }

        return new ExperimentalFeatureListResult
        {
            Data = entries,
            NextCursor = CodexAppServerClientJson.GetStringOrNull(result, "nextCursor"),
            Raw = result
        };
    }

    private static IReadOnlyList<ModelReasoningEffortOption> ParseReasoningEfforts(JsonElement item)
    {
        var efforts = CodexAppServerClientJson.TryGetArray(item, "supportedReasoningEfforts");
        if (efforts is null)
        {
            return Array.Empty<ModelReasoningEffortOption>();
        }

        var parsed = new List<ModelReasoningEffortOption>();
        foreach (var effort in efforts.Value.EnumerateArray())
        {
            if (effort.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var reasoningEffort = CodexAppServerClientJson.GetStringOrNull(effort, "reasoningEffort");
            if (string.IsNullOrWhiteSpace(reasoningEffort))
            {
                continue;
            }

            parsed.Add(new ModelReasoningEffortOption
            {
                ReasoningEffort = reasoningEffort,
                Description = CodexAppServerClientJson.GetStringOrNull(effort, "description") ?? string.Empty
            });
        }

        return parsed;
    }

    private static ModelUpgradeInfo? ParseUpgradeInfo(JsonElement item)
    {
        var upgradeInfo = CodexAppServerClientJson.TryGetObject(item, "upgradeInfo");
        if (upgradeInfo is null)
        {
            return null;
        }

        var upgrade = upgradeInfo.Value;
        var model = CodexAppServerClientJson.GetStringOrNull(upgrade, "model");
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        return new ModelUpgradeInfo
        {
            Model = model,
            UpgradeCopy = CodexAppServerClientJson.GetStringOrNull(upgrade, "upgradeCopy"),
            ModelLink = CodexAppServerClientJson.GetStringOrNull(upgrade, "modelLink"),
            MigrationMarkdown = CodexAppServerClientJson.GetStringOrNull(upgrade, "migrationMarkdown")
        };
    }

    private static string MapMergeStrategyValue(ConfigMergeStrategy strategy) =>
        strategy switch
        {
            ConfigMergeStrategy.Replace => "replace",
            ConfigMergeStrategy.Upsert => "upsert",
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown config merge strategy.")
        };

    private static void ValidateConfigValueWriteOptions(ConfigValueWriteOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.KeyPath))
        {
            throw new ArgumentException("KeyPath cannot be empty or whitespace.", nameof(options));
        }

        if (options.Value.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("Value must be a defined JSON value.", nameof(options));
        }
    }

    private static void ValidateConfigBatchWriteOptions(ConfigBatchWriteOptions options)
    {
        if (options.Edits.Count == 0)
        {
            throw new ArgumentException("Edits cannot be empty.", nameof(options));
        }

        for (var i = 0; i < options.Edits.Count; i++)
        {
            var edit = options.Edits[i];
            if (edit is null)
            {
                throw new ArgumentException($"Edit at index {i} cannot be null.", nameof(options));
            }

            if (string.IsNullOrWhiteSpace(edit.KeyPath))
            {
                throw new ArgumentException($"Edit at index {i} must have a non-empty KeyPath.", nameof(options));
            }

            if (edit.Value.ValueKind == JsonValueKind.Undefined)
            {
                throw new ArgumentException($"Edit at index {i} must have a defined JSON Value.", nameof(options));
            }
        }
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
