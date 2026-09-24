using System.Linq;
using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientSkillsAppsParsers
{
    public static IReadOnlyList<SkillsListEntryResult> ParseSkillsListEntries(JsonElement skillsListResult)
    {
        var data = TryGetArray(skillsListResult, JsonFieldNames.Data);
        if (data is not null && data.Value.ValueKind == JsonValueKind.Array)
        {
            var entries = new List<SkillsListEntryResult>();
            foreach (var entry in data.Value.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var cwd = GetStringOrNull(entry, JsonFieldNames.Cwd);

                var skills = new List<SkillDescriptor>();
                var skillsArray = TryGetArray(entry, JsonFieldNames.Skills);
                if (skillsArray is not null && skillsArray.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var skill in skillsArray.Value.EnumerateArray())
                    {
                        if (skill.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var name = GetStringOrNull(skill, JsonFieldNames.Name) ?? GetStringOrNull(skill, JsonFieldNames.Id);
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        skills.Add(new SkillDescriptor
                        {
                            Name = name,
                            Description = GetStringOrNull(skill, JsonFieldNames.Description),
                            ShortDescription = GetStringOrNull(skill, JsonFieldNames.ShortDescription),
                            Path = GetStringOrNull(skill, JsonFieldNames.Path),
                            Enabled = GetBoolOrNull(skill, JsonFieldNames.Enabled),
                            Cwd = cwd,
                            Scope = GetStringOrNull(skill, JsonFieldNames.Scope),
                            Dependencies = TryGetObject(skill, JsonFieldNames.Dependencies),
                            Interface = TryGetObject(skill, JsonFieldNames.Interface),
                            Raw = skill
                        });
                    }
                }

                var errors = new List<CodexSkillErrorInfo>();
                var errorsArray = TryGetArray(entry, "errors");
                if (errorsArray is not null && errorsArray.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var err in errorsArray.Value.EnumerateArray())
                    {
                        if (err.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        errors.Add(new CodexSkillErrorInfo
                        {
                            Message = GetStringOrNull(err, JsonFieldNames.Message),
                            Path = GetStringOrNull(err, JsonFieldNames.Path),
                            Raw = err
                        });
                    }
                }

                entries.Add(new SkillsListEntryResult
                {
                    Cwd = cwd,
                    Skills = skills,
                    Errors = errors,
                    Raw = entry
                });
            }

            return entries;
        }

        var legacySkills = TryGetArray(skillsListResult, JsonFieldNames.Skills) ?? TryGetArray(skillsListResult, JsonFieldNames.Items);
        if (legacySkills is not null && legacySkills.Value.ValueKind == JsonValueKind.Array)
        {
            var skills = new List<SkillDescriptor>();
            foreach (var item in legacySkills.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = GetStringOrNull(item, JsonFieldNames.Name) ?? GetStringOrNull(item, JsonFieldNames.Id);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                skills.Add(new SkillDescriptor
                {
                    Name = name,
                    Description = GetStringOrNull(item, JsonFieldNames.Description),
                    ShortDescription = GetStringOrNull(item, JsonFieldNames.ShortDescription),
                    Path = GetStringOrNull(item, JsonFieldNames.Path),
                    Enabled = GetBoolOrNull(item, JsonFieldNames.Enabled),
                    Scope = GetStringOrNull(item, JsonFieldNames.Scope),
                    Dependencies = TryGetObject(item, JsonFieldNames.Dependencies),
                    Interface = TryGetObject(item, JsonFieldNames.Interface),
                    Raw = item
                });
            }

            return
            [
                new SkillsListEntryResult
                {
                    Cwd = null,
                    Skills = skills,
                    Errors = Array.Empty<CodexSkillErrorInfo>(),
                    Raw = skillsListResult
                }
            ];
        }

        return Array.Empty<SkillsListEntryResult>();
    }

    public static IReadOnlyList<SkillDescriptor> ParseSkillsListSkills(JsonElement skillsListResult)
    {
        var entries = ParseSkillsListEntries(skillsListResult);
        return ParseSkillsListSkills(entries);
    }

    public static IReadOnlyList<SkillDescriptor> ParseSkillsListSkills(IReadOnlyList<SkillsListEntryResult> entries)
    {
        if (entries.Count == 0)
        {
            return Array.Empty<SkillDescriptor>();
        }

        return entries.SelectMany(e => e.Skills).ToArray();
    }

    public static IReadOnlyList<AppDescriptor> ParseAppsListApps(JsonElement appsListResult)
    {
        var array =
            TryGetArray(appsListResult, JsonFieldNames.Data) ??
            TryGetArray(appsListResult, JsonFieldNames.Apps) ??
            TryGetArray(appsListResult, JsonFieldNames.Items);

        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<AppDescriptor>();
        }

        var apps = new List<AppDescriptor>();
        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            apps.Add(new AppDescriptor
            {
                Id = GetStringOrNull(item, JsonFieldNames.Id),
                Name = GetStringOrNull(item, JsonFieldNames.Name),
                Description = GetStringOrNull(item, JsonFieldNames.Description),
                LogoUrl = GetStringOrNull(item, JsonFieldNames.LogoUrl) ?? GetStringOrNull(item, "logo_url"),
                LogoUrlDark = GetStringOrNull(item, JsonFieldNames.LogoUrlDark) ?? GetStringOrNull(item, "logo_url_dark"),
                DistributionChannel = GetStringOrNull(item, JsonFieldNames.DistributionChannel),
                AppMetadata = TryGetObject(item, "appMetadata"),
                Branding = TryGetObject(item, "branding"),
                InstallUrl = GetStringOrNull(item, JsonFieldNames.InstallUrl),
                IsAccessible = GetBoolOrNull(item, "isAccessible"),
                IsEnabled = GetBoolOrNull(item, JsonFieldNames.IsEnabled) ?? GetBoolOrNull(item, JsonFieldNames.Enabled),
                Title = GetStringOrNull(item, JsonFieldNames.Title),
                PluginDisplayNames = GetOptionalStringArray(item, JsonFieldNames.PluginDisplayNames) ?? Array.Empty<string>(),
                Labels = ParseStringMap(item, "labels"),
                DisabledReason = GetStringOrNull(item, JsonFieldNames.DisabledReason),
                Raw = item
            });
        }

        return apps;
    }

    public static IReadOnlyList<AppConnectorMetadata> ParseAppsReadApps(JsonElement appsReadResult)
    {
        var array = TryGetArray(appsReadResult, JsonFieldNames.Apps)
            ?? throw new InvalidOperationException("app/read returned no apps array.");

        var apps = new List<AppConnectorMetadata>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("app/read apps[] entries must be objects.");
            }

            apps.Add(new AppConnectorMetadata
            {
                Id = GetRequiredString(item, JsonFieldNames.Id, "app/read apps[]"),
                Name = GetRequiredString(item, JsonFieldNames.Name, "app/read apps[]"),
                Description = GetStringOrNull(item, JsonFieldNames.Description),
                IconUrl = GetStringOrNull(item, "iconUrl"),
                IconUrlDark = GetStringOrNull(item, "iconUrlDark"),
                DistributionChannel = GetStringOrNull(item, JsonFieldNames.DistributionChannel),
                InstallUrl = GetStringOrNull(item, JsonFieldNames.InstallUrl),
                PluginDisplayNames = GetOptionalStringArray(item, JsonFieldNames.PluginDisplayNames) ?? Array.Empty<string>(),
                ToolSummaries = ParseAppToolSummaries(item),
                Raw = item.Clone()
            });
        }

        return apps;
    }

    public static IReadOnlyList<InstalledAppDescriptor> ParseInstalledApps(JsonElement appsInstalledResult)
    {
        var array = TryGetArray(appsInstalledResult, JsonFieldNames.Apps)
            ?? throw new InvalidOperationException("app/installed returned no apps array.");

        var apps = new List<InstalledAppDescriptor>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("app/installed apps[] entries must be objects.");
            }

            apps.Add(new InstalledAppDescriptor
            {
                Id = GetRequiredString(item, JsonFieldNames.Id, "app/installed apps[]"),
                RuntimeName = GetStringOrNull(item, "runtimeName"),
                Enabled = GetRequiredBool(item, JsonFieldNames.Enabled, "app/installed apps[]"),
                Callable = GetRequiredBool(item, "callable", "app/installed apps[]"),
                Raw = item.Clone()
            });
        }

        return apps;
    }

    public static IReadOnlyList<RemoteSkillDescriptor> ParseRemoteSkillsReadSkills(JsonElement remoteSkillsResult)
    {
        var array =
            TryGetArray(remoteSkillsResult, JsonFieldNames.Data) ??
            TryGetArray(remoteSkillsResult, JsonFieldNames.Skills);

        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RemoteSkillDescriptor>();
        }

        var skills = new List<RemoteSkillDescriptor>();
        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = GetStringOrNull(item, JsonFieldNames.Id);
            var name = GetStringOrNull(item, JsonFieldNames.Name);
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            skills.Add(new RemoteSkillDescriptor
            {
                Id = id,
                Name = name,
                Description = GetStringOrNull(item, JsonFieldNames.Description),
                Raw = item
            });
        }

        return skills;
    }

    private static IReadOnlyDictionary<string, string>? ParseStringMap(JsonElement obj, string propertyName)
    {
        if (TryGetObject(obj, propertyName) is not { } values)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in values.EnumerateObject())
        {
            if (item.Value.ValueKind == JsonValueKind.String)
            {
                result[item.Name] = item.Value.GetString() ?? string.Empty;
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyList<AppToolSummaryDescriptor> ParseAppToolSummaries(JsonElement item)
    {
        var array = TryGetArray(item, "toolSummaries");
        if (array is null)
        {
            return Array.Empty<AppToolSummaryDescriptor>();
        }

        var tools = new List<AppToolSummaryDescriptor>();
        foreach (var tool in array.Value.EnumerateArray())
        {
            if (tool.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("app/read toolSummaries[] entries must be objects.");
            }

            tools.Add(new AppToolSummaryDescriptor
            {
                Name = GetRequiredString(tool, JsonFieldNames.Name, "app/read toolSummaries[]"),
                Title = GetStringOrNull(tool, JsonFieldNames.Title),
                Description = GetRequiredString(tool, JsonFieldNames.Description, "app/read toolSummaries[]"),
                IsEnabled = GetBoolOrNull(tool, JsonFieldNames.IsEnabled) ?? true,
                DisabledReason = GetStringOrNull(tool, JsonFieldNames.DisabledReason),
                IsReadOnly = GetBoolOrNull(tool, "isReadOnly") ?? false,
                Raw = tool.Clone()
            });
        }

        return tools;
    }
}
