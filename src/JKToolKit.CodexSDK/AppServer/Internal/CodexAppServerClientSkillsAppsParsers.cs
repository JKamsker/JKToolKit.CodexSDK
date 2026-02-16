using System.Linq;
using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientSkillsAppsParsers
{
    public static IReadOnlyList<SkillsListEntryResult> ParseSkillsListEntries(JsonElement skillsListResult)
    {
        var data = TryGetArray(skillsListResult, "data");
        if (data is not null && data.Value.ValueKind == JsonValueKind.Array)
        {
            var entries = new List<SkillsListEntryResult>();
            foreach (var entry in data.Value.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var cwd = GetStringOrNull(entry, "cwd");

                var skills = new List<SkillDescriptor>();
                var skillsArray = TryGetArray(entry, "skills");
                if (skillsArray is not null && skillsArray.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var skill in skillsArray.Value.EnumerateArray())
                    {
                        if (skill.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var name = GetStringOrNull(skill, "name") ?? GetStringOrNull(skill, "id");
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        skills.Add(new SkillDescriptor
                        {
                            Name = name,
                            Description = GetStringOrNull(skill, "description"),
                            ShortDescription = GetStringOrNull(skill, "shortDescription"),
                            Path = GetStringOrNull(skill, "path"),
                            Enabled = GetBoolOrNull(skill, "enabled"),
                            Cwd = cwd,
                            Scope = GetStringOrNull(skill, "scope"),
                            Dependencies = TryGetObject(skill, "dependencies"),
                            Interface = TryGetObject(skill, "interface"),
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
                            Message = GetStringOrNull(err, "message"),
                            Path = GetStringOrNull(err, "path"),
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

        var legacySkills = TryGetArray(skillsListResult, "skills") ?? TryGetArray(skillsListResult, "items");
        if (legacySkills is not null && legacySkills.Value.ValueKind == JsonValueKind.Array)
        {
            var skills = new List<SkillDescriptor>();
            foreach (var item in legacySkills.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = GetStringOrNull(item, "name") ?? GetStringOrNull(item, "id");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                skills.Add(new SkillDescriptor
                {
                    Name = name,
                    Description = GetStringOrNull(item, "description"),
                    ShortDescription = GetStringOrNull(item, "shortDescription"),
                    Path = GetStringOrNull(item, "path"),
                    Enabled = GetBoolOrNull(item, "enabled"),
                    Scope = GetStringOrNull(item, "scope"),
                    Dependencies = TryGetObject(item, "dependencies"),
                    Interface = TryGetObject(item, "interface"),
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
            TryGetArray(appsListResult, "data") ??
            TryGetArray(appsListResult, "apps") ??
            TryGetArray(appsListResult, "items");

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
                Id = GetStringOrNull(item, "id"),
                Name = GetStringOrNull(item, "name"),
                Description = GetStringOrNull(item, "description"),
                LogoUrl = GetStringOrNull(item, "logoUrl") ?? GetStringOrNull(item, "logo_url"),
                LogoUrlDark = GetStringOrNull(item, "logoUrlDark") ?? GetStringOrNull(item, "logo_url_dark"),
                DistributionChannel = GetStringOrNull(item, "distributionChannel"),
                InstallUrl = GetStringOrNull(item, "installUrl"),
                IsAccessible = GetBoolOrNull(item, "isAccessible"),
                IsEnabled = GetBoolOrNull(item, "isEnabled") ?? GetBoolOrNull(item, "enabled"),
                Title = GetStringOrNull(item, "title"),
                DisabledReason = GetStringOrNull(item, "disabledReason"),
                Raw = item
            });
        }

        return apps;
    }

    public static IReadOnlyList<RemoteSkillDescriptor> ParseRemoteSkillsReadSkills(JsonElement remoteSkillsResult)
    {
        var array =
            TryGetArray(remoteSkillsResult, "data") ??
            TryGetArray(remoteSkillsResult, "skills");

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

            var id = GetStringOrNull(item, "id");
            var name = GetStringOrNull(item, "name");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            skills.Add(new RemoteSkillDescriptor
            {
                Id = id,
                Name = name,
                Description = GetStringOrNull(item, "description"),
                Raw = item
            });
        }

        return skills;
    }
}

