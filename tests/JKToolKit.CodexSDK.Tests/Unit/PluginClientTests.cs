using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class PluginClientTests
{
    [Fact]
    public async Task ListPluginsAsync_ParsesTypedMarketplacePluginAndSourceMetadata()
    {
        var marketPath = XPaths.JsonAbs("market");
        var iconPath = XPaths.JsonAbs("market/assets/icon.png");
        var logoPath = XPaths.JsonAbs("market/assets/logo.png");
        var screenshotPath = XPaths.JsonAbs("market/assets/screenshot.png");
        var sourcePath = XPaths.JsonAbs("plugins/plug-1");
        using var doc = JsonDocument.Parse(
            $@"{{
              ""featuredPluginIds"": [""plug-1""],
              ""marketplaceLoadErrors"": [{{ ""marketplacePath"": ""{marketPath}"", ""message"": ""failed"" }}],
              ""marketplaces"": [
                {{
                  ""name"": ""official"",
                  ""path"": ""{marketPath}"",
                  ""interface"": {{ ""displayName"": ""Official Marketplace"" }},
                  ""plugins"": [
                    {{
                      ""id"": ""plug-1"",
                      ""name"": ""Plugin One"",
                      ""installed"": true,
                      ""enabled"": true,
                      ""authPolicy"": ""ON_INSTALL"",
                      ""installPolicy"": ""AVAILABLE"",
                      ""interface"": {{
                        ""displayName"": ""Plugin One Display"",
                        ""shortDescription"": ""Short"",
                        ""longDescription"": ""Long"",
                        ""category"": ""developer-tools"",
                        ""developerName"": ""Example Corp"",
                        ""brandColor"": ""#123456"",
                        ""defaultPrompt"": [""Open a pull request""],
                        ""capabilities"": [""issues"", ""pull-requests""],
                        ""composerIcon"": ""{iconPath}"",
                        ""composerIconUrl"": ""https://example.test/icon.png"",
                        ""logo"": ""{logoPath}"",
                        ""logoUrl"": ""https://example.test/logo.png"",
                        ""screenshots"": [""{screenshotPath}""],
                        ""screenshotUrls"": [""https://example.test/screenshot.png""]
                      }},
                      ""availability"": ""ENABLED"",
                      ""keywords"": [""review"", ""git""],
                      ""source"": {{
                        ""type"": ""local"",
                        ""path"": ""{sourcePath}""
                      }}
                    }}
                  ]
                }}
              ]
            }}");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.ListPluginsAsync(new PluginListOptions { ForceRemoteSync = true });

        result.FeaturedPluginIds.Should().Equal("plug-1");
        result.MarketplaceLoadErrors.Should().ContainSingle();
        result.Marketplaces.Should().ContainSingle();
        result.Marketplaces[0].Interface.Should().NotBeNull();
        result.Marketplaces[0].Interface!.DisplayName.Should().Be("Official Marketplace");
        result.Marketplaces[0].Plugins.Should().ContainSingle();
        result.Marketplaces[0].Plugins[0].Id.Should().Be("plug-1");
        result.Marketplaces[0].Plugins[0].AuthPolicyValue.Should().Be(PluginAuthPolicy.OnInstall);
        result.Marketplaces[0].Plugins[0].InstallPolicyValue.Should().Be(PluginInstallPolicy.Available);
        result.Marketplaces[0].Plugins[0].Interface.Should().NotBeNull();
        result.Marketplaces[0].Plugins[0].Interface!.DisplayName.Should().Be("Plugin One Display");
        result.Marketplaces[0].Plugins[0].Interface!.Capabilities.Should().Equal("issues", "pull-requests");
        result.Marketplaces[0].Plugins[0].Interface!.ComposerIconPath.Should().Be(iconPath);
        result.Marketplaces[0].Plugins[0].Interface!.ComposerIconUrl.Should().Be("https://example.test/icon.png");
        result.Marketplaces[0].Plugins[0].Interface!.LogoPath.Should().Be(logoPath);
        result.Marketplaces[0].Plugins[0].Interface!.LogoUrl.Should().Be("https://example.test/logo.png");
        result.Marketplaces[0].Plugins[0].Interface!.Screenshots.Should().Equal(screenshotPath);
        result.Marketplaces[0].Plugins[0].Interface!.ScreenshotUrls.Should().Equal("https://example.test/screenshot.png");
        result.Marketplaces[0].Plugins[0].AvailabilityValue.Should().Be(PluginAvailability.Available);
        result.Marketplaces[0].Plugins[0].Keywords.Should().Equal("review", "git");
        result.Marketplaces[0].Plugins[0].SourceInfo.Should().NotBeNull();
        result.Marketplaces[0].Plugins[0].SourceInfo!.Type.Should().Be(PluginSourceType.Local);
        result.Marketplaces[0].Plugins[0].SourceInfo!.Path.Should().Be(sourcePath);
        result.Marketplaces[0].Plugins[0].Source.TryGetProperty("path", out var sourcePathElem).Should().BeTrue();
        sourcePathElem.GetString().Should().Be(sourcePath);
        rpc.LastMethod.Should().Be("plugin/list");
    }

    [Fact]
    public async Task ListPluginsAsync_DefaultsOptionalArraysToEmpty()
    {
        var marketPath = XPaths.JsonAbs("market");
        using var doc = JsonDocument.Parse($@"{{""marketplaces"":[{{""name"":""official"",""path"":""{marketPath}"",""plugins"":[]}}]}}");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.ListPluginsAsync(new PluginListOptions());

        result.FeaturedPluginIds.Should().BeEmpty();
        result.MarketplaceLoadErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task ListPluginsAsync_SendsMarketplaceKinds_AndOmitsLegacyForceRemoteSync()
    {
        using var doc = JsonDocument.Parse("""{"marketplaces":[]}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        await client.ListPluginsAsync(new PluginListOptions
        {
            ForceRemoteSync = true,
            MarketplaceKinds = [PluginListMarketplaceKind.Local, PluginListMarketplaceKind.SharedWithMe]
        });

        var json = JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().Contain("\"marketplaceKinds\":[\"local\",\"shared-with-me\"]");
        json.Should().NotContain("forceRemoteSync");
    }

    [Fact]
    public async Task ListPluginsAsync_ParsesRemoteMarketplaceAndShareMetadata()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "marketplaces": [
                {
                  "name": "remote",
                  "path": null,
                  "plugins": [
                    {
                      "id": "plug-remote",
                      "remotePluginId": "remote-1",
                      "version": "4.5.6",
                      "localVersion": "1.2.3",
                      "name": "Remote Plugin",
                      "installed": false,
                      "enabled": true,
                      "authPolicy": "ON_USE",
                      "installPolicy": "AVAILABLE",
                      "installPolicySource": "IMPLICIT_CANONICAL_APP",
                      "availability": "AVAILABLE",
                      "keywords": ["remote"],
                      "shareContext": {
                        "remotePluginId": "remote-1",
                        "remoteVersion": "4.5.6",
                        "discoverability": "LISTED",
                        "shareUrl": "https://example.test/share",
                        "creatorAccountUserId": "user-1",
                        "creatorName": "Ada",
                        "sharePrincipals": [
                          {
                            "principalType": "user",
                            "principalId": "user-2",
                            "role": "reader",
                            "name": "Grace"
                          }
                        ]
                      },
                      "interface": {
                        "displayName": "Remote Plugin",
                        "capabilities": [],
                        "screenshots": [],
                        "screenshotUrls": ["https://example.test/shot.png"],
                        "composerIconUrl": "https://example.test/icon.png",
                        "logoUrl": "https://example.test/logo.png"
                      },
                      "source": {
                        "type": "remote"
                      }
                    }
                  ]
                }
              ]
            }
            """);
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.ListPluginsAsync(new PluginListOptions());

        var marketplace = result.Marketplaces.Should().ContainSingle().Subject;
        marketplace.Path.Should().BeNull();
        var plugin = marketplace.Plugins.Should().ContainSingle().Subject;
        plugin.RemotePluginId.Should().Be("remote-1");
        plugin.Version.Should().Be("4.5.6");
        plugin.LocalVersion.Should().Be("1.2.3");
        plugin.InstallPolicySource.Should().Be("IMPLICIT_CANONICAL_APP");
        plugin.InstallPolicySourceValue.Should().Be(PluginInstallPolicySource.ImplicitCanonicalApp);
        plugin.SourceInfo.Type.Should().Be(PluginSourceType.Remote);
        plugin.SourceInfo.Path.Should().BeNull();
        plugin.ShareContext.Should().NotBeNull();
        plugin.ShareContext!.RemoteVersion.Should().Be("4.5.6");
        plugin.ShareContext.Discoverability.Should().Be(PluginShareDiscoverability.Listed);
        plugin.ShareContext.SharePrincipals.Should().ContainSingle()
            .Which.Role.Should().Be(PluginSharePrincipalRole.Reader);
        plugin.Interface!.ScreenshotUrls.Should().Equal("https://example.test/shot.png");
    }

    [Fact]
    public async Task ListPluginsAsync_MissingMarketplaces_Throws()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var act = async () => await client.ListPluginsAsync(new PluginListOptions());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*marketplaces*");
    }

    [Fact]
    public async Task ListPluginsAsync_MissingMarketplacePlugins_Throws()
    {
        var marketPath = XPaths.JsonAbs("market");
        using var doc = JsonDocument.Parse(
            $$"""
            {"marketplaces":[{"name":"official","path":"{{marketPath}}"}]}
            """);
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var act = async () => await client.ListPluginsAsync(new PluginListOptions());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*plugins*");
    }

    [Fact]
    public async Task ReadPluginAsync_ParsesDetailSurfaceAndSkillInterfaceMetadata()
    {
        var marketPath = XPaths.JsonAbs("market");
        var pluginSourcePath = XPaths.JsonAbs("plugins/plug-1");
        var skillPath = XPaths.JsonAbs("skills/a");
        using var doc = JsonDocument.Parse(
            $@"{{
              ""plugin"": {{
                ""description"": ""desc"",
                ""marketplaceName"": ""official"",
                ""marketplacePath"": ""{marketPath}"",
                ""skills"": [
                  {{
                    ""name"": ""skill-a"",
                    ""path"": ""{skillPath}"",
                    ""enabled"": true,
                    ""description"": ""desc"",
                    ""interface"": {{
                      ""displayName"": ""Skill A"",
                      ""brandColor"": ""#abcdef"",
                      ""defaultPrompt"": ""Explain the failing build"",
                      ""iconLarge"": ""https://example.test/skill-large.png"",
                      ""iconSmall"": ""https://example.test/skill-small.png"",
                      ""shortDescription"": ""Skill short""
                    }}
                  }}
                ],
                ""summary"": {{
                  ""id"": ""plug-1"",
                  ""name"": ""Plugin One"",
                  ""installed"": true,
                  ""enabled"": true,
                  ""authPolicy"": ""ON_USE"",
                  ""installPolicy"": ""INSTALLED_BY_DEFAULT"",
                  ""source"": {{
                    ""type"": ""local"",
                    ""path"": ""{pluginSourcePath}""
                  }},
                  ""interface"": {{
                    ""displayName"": ""Plugin One Display"",
                    ""capabilities"": [],
                    ""screenshots"": []
                  }}
                }},
                ""apps"": [],
                ""hooks"": [
                  {{
                    ""key"": ""pre-tool"",
                    ""eventName"": ""PreToolUse""
                  }}
                ],
                ""mcpServers"": []
              }}
            }}");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.ReadPluginAsync(new PluginReadOptions
        {
            MarketplacePath = XPaths.Abs("market"),
            PluginName = "plugin-one"
        });

        result.Plugin.Summary.Id.Should().Be("plug-1");
        result.Plugin.Summary.AuthPolicyValue.Should().Be(PluginAuthPolicy.OnUse);
        result.Plugin.Summary.InstallPolicyValue.Should().Be(PluginInstallPolicy.InstalledByDefault);
        result.Plugin.Summary.Interface.Should().NotBeNull();
        result.Plugin.Summary.Interface!.DisplayName.Should().Be("Plugin One Display");
        result.Plugin.McpServers.Should().BeEmpty();
        result.Plugin.Skills.Should().ContainSingle();
        result.Plugin.Skills[0].Path.Should().Be(skillPath);
        result.Plugin.Skills[0].Interface.Should().NotBeNull();
        result.Plugin.Skills[0].Interface!.DisplayName.Should().Be("Skill A");
        result.Plugin.Skills[0].Interface!.DefaultPrompt.Should().Be("Explain the failing build");
        result.Plugin.Apps.Should().BeEmpty();
        result.Plugin.Hooks.Should().ContainSingle();
        result.Plugin.Hooks[0].Key.Should().Be("pre-tool");
        result.Plugin.Hooks[0].EventName.Should().Be("PreToolUse");
        result.Plugin.McpServers.Should().BeEmpty();
        rpc.LastMethod.Should().Be("plugin/read");
    }

    [Fact]
    public async Task ReadPluginAsync_MissingRequiredSkillFields_Throws()
    {
        var marketPath = XPaths.JsonAbs("market");
        var sourcePath = XPaths.JsonAbs("plugins/plug-1");
        using var doc = JsonDocument.Parse(
            $@"{{
              ""plugin"": {{
                ""marketplaceName"": ""official"",
                ""marketplacePath"": ""{marketPath}"",
                ""skills"": [
                  {{
                    ""name"": ""skill-a"",
                    ""enabled"": true
                  }}
                ],
                ""summary"": {{
                  ""id"": ""plug-1"",
                  ""name"": ""Plugin One"",
                  ""installed"": true,
                  ""enabled"": true,
                  ""authPolicy"": ""ON_USE"",
                  ""installPolicy"": ""INSTALLED_BY_DEFAULT"",
                  ""source"": {{
                    ""type"": ""local"",
                    ""path"": ""{sourcePath}""
                  }}
                }},
                ""apps"": [],
                ""mcpServers"": []
              }}
            }}");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var act = async () => await client.ReadPluginAsync(new PluginReadOptions
        {
            MarketplacePath = XPaths.Abs("market"),
            PluginName = "plugin-one"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*description*");
    }

    [Fact]
    public async Task ReadPluginAsync_MissingRequiredCollections_Throws()
    {
        var marketPath = XPaths.JsonAbs("market");
        var sourcePath = XPaths.JsonAbs("plugins/plug-1");
        using var doc = JsonDocument.Parse(
            $$"""
            {
              "plugin": {
                "marketplaceName": "official",
                "marketplacePath": "{{marketPath}}",
                "summary": {
                  "id": "plug-1",
                  "name": "Plugin One",
                  "installed": true,
                  "enabled": true,
                  "authPolicy": "ON_USE",
                  "installPolicy": "INSTALLED_BY_DEFAULT",
                  "source": {
                    "type": "local",
                    "path": "{{sourcePath}}"
                  }
                }
              }
            }
            """);
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var act = async () => await client.ReadPluginAsync(new PluginReadOptions
        {
            MarketplacePath = XPaths.Abs("market"),
            PluginName = "plugin-one"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*skills*");
    }

    [Fact]
    public async Task ReadPluginAsync_InterfaceMissingOptionalCollections_DefaultsToEmpty()
    {
        var marketPath = XPaths.JsonAbs("market");
        var sourcePath = XPaths.JsonAbs("plugins/plug-1");
        using var doc = JsonDocument.Parse(
            $$"""
            {
              "plugin": {
                "marketplaceName": "official",
                "marketplacePath": "{{marketPath}}",
                "skills": [],
                "apps": [],
                "mcpServers": [],
                "summary": {
                  "id": "plug-1",
                  "name": "Plugin One",
                  "installed": true,
                  "enabled": true,
                  "authPolicy": "ON_USE",
                  "installPolicy": "INSTALLED_BY_DEFAULT",
                  "source": {
                    "type": "local",
                    "path": "{{sourcePath}}"
                  },
                  "interface": {
                    "displayName": "Plugin One Display"
                  }
                }
              }
            }
            """);
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.ReadPluginAsync(new PluginReadOptions
        {
            MarketplacePath = XPaths.Abs("market"),
            PluginName = "plugin-one"
        });

        result.Plugin.Summary.Interface.Should().NotBeNull();
        result.Plugin.Summary.Interface!.Capabilities.Should().BeEmpty();
        result.Plugin.Summary.Interface.Screenshots.Should().BeEmpty();
        result.Plugin.Summary.Interface.ScreenshotUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task InstallPluginAsync_ParsesTypedAuthPolicyAndDefaultsAppsNeedingAuth()
    {
        using var doc = JsonDocument.Parse("""{"authPolicy":"ON_INSTALL","appsNeedingAuth":[]}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.InstallPluginAsync(new PluginInstallOptions
        {
            MarketplacePath = XPaths.Abs("market"),
            PluginName = "plugin-one"
        });

        result.AuthPolicy.Should().Be("ON_INSTALL");
        result.AuthPolicyValue.Should().Be(PluginAuthPolicy.OnInstall);
        result.AppsNeedingAuth.Should().BeEmpty();
        rpc.LastMethod.Should().Be("plugin/install");
    }

    [Fact]
    public async Task ReadAndInstallPluginAsync_AcceptRemoteMarketplaceName()
    {
        var sourcePath = XPaths.JsonAbs("plugins/plug-1");
        using var readDoc = JsonDocument.Parse(
            $@"{{
              ""plugin"": {{
                ""marketplaceName"": ""remote"",
                ""marketplacePath"": null,
                ""skills"": [],
                ""apps"": [],
                ""hooks"": [],
                ""mcpServers"": [],
                ""summary"": {{
                  ""id"": ""plug-1"",
                  ""name"": ""Plugin One"",
                  ""installed"": true,
                  ""enabled"": true,
                  ""authPolicy"": ""ON_USE"",
                  ""installPolicy"": ""AVAILABLE"",
                  ""source"": {{ ""type"": ""local"", ""path"": ""{sourcePath}"" }}
                }}
              }}
            }}");
        var readRpc = new RecordingRpc { Result = readDoc.RootElement };
        await using var readClient = CreateClient(readRpc);

        await readClient.ReadPluginAsync(new PluginReadOptions
        {
            RemoteMarketplaceName = "codex-remote",
            PluginName = "plugin-one"
        });

        var readJson = JsonSerializer.Serialize(readRpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        readJson.Should().Contain("\"remoteMarketplaceName\":\"codex-remote\"");
        readJson.Should().NotContain("forceRemoteSync");

        using var installDoc = JsonDocument.Parse("""{"authPolicy":"ON_USE","appsNeedingAuth":[]}""");
        var installRpc = new RecordingRpc { Result = installDoc.RootElement };
        await using var installClient = CreateClient(installRpc);

        await installClient.InstallPluginAsync(new PluginInstallOptions
        {
            RemoteMarketplaceName = "codex-remote",
            PluginName = "plugin-one",
            ForceRemoteSync = true
        });

        var installJson = JsonSerializer.Serialize(installRpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        installJson.Should().Contain("\"remoteMarketplaceName\":\"codex-remote\"");
        installJson.Should().NotContain("forceRemoteSync");
    }

    [Fact]
    public async Task ReadPluginAsync_RequiresExactlyOneMarketplaceSelector()
    {
        var rpc = new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement };
        await using var client = CreateClient(rpc);

        var missing = async () => await client.ReadPluginAsync(new PluginReadOptions { PluginName = "plugin-one" });
        await missing.Should().ThrowAsync<ArgumentException>().WithMessage("*Exactly one*");

        var both = async () => await client.ReadPluginAsync(new PluginReadOptions
        {
            MarketplacePath = XPaths.Abs("market"),
            RemoteMarketplaceName = "remote",
            PluginName = "plugin-one"
        });
        await both.Should().ThrowAsync<ArgumentException>().WithMessage("*Exactly one*");
        rpc.LastMethod.Should().BeNull();
    }

    [Fact]
    public async Task PluginShareMethods_SendExpectedParams_AndParseResponses()
    {
        using var saveDoc = JsonDocument.Parse("""{"remotePluginId":"remote-1","shareUrl":"https://example.test/share"}""");
        var saveRpc = new RecordingRpc { Result = saveDoc.RootElement };
        await using var saveClient = CreateClient(saveRpc);

        var save = await saveClient.SavePluginShareAsync(new PluginShareSaveOptions
        {
            PluginPath = XPaths.Abs("plugins/plug-1"),
            Discoverability = PluginShareDiscoverability.Unlisted,
            ShareTargets =
            [
                new PluginShareTarget
                {
                    PrincipalType = PluginSharePrincipalType.User,
                    PrincipalId = "user-1",
                    Role = PluginShareTargetRole.Reader
                }
            ]
        });

        save.RemotePluginId.Should().Be("remote-1");
        save.ShareUrl.Should().Be("https://example.test/share");
        JsonSerializer.Serialize(saveRpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"discoverability\":\"UNLISTED\"")
            .And.Contain("\"principalType\":\"user\"");

        using var updateDoc = JsonDocument.Parse(
            """
            {
              "principals": [
                {
                  "principalType": "user",
                  "principalId": "user-1",
                  "role": "owner",
                  "name": "Ada"
                }
              ],
              "discoverability": "PRIVATE"
            }
            """);
        var updateRpc = new RecordingRpc { Result = updateDoc.RootElement };
        await using var updateClient = CreateClient(updateRpc);

        var update = await updateClient.UpdatePluginShareTargetsAsync(new PluginShareUpdateTargetsOptions
        {
            RemotePluginId = "remote-1",
            Discoverability = PluginShareUpdateDiscoverability.Private,
            ShareTargets = []
        });

        update.Discoverability.Should().Be(PluginShareDiscoverability.Private);
        update.Principals.Should().ContainSingle().Which.Role.Should().Be(PluginSharePrincipalRole.Owner);
        updateRpc.LastMethod.Should().Be("plugin/share/updateTargets");

        using var deleteDoc = JsonDocument.Parse("""{}""");
        var deleteRpc = new RecordingRpc { Result = deleteDoc.RootElement };
        await using var deleteClient = CreateClient(deleteRpc);

        await deleteClient.DeletePluginShareAsync(new PluginShareDeleteOptions { RemotePluginId = "remote-1" });

        deleteRpc.LastMethod.Should().Be("plugin/share/delete");
    }

    [Fact]
    public async Task PluginShareListAndCheckout_ParseResponses()
    {
        using var listDoc = JsonDocument.Parse(
            $@"{{
              ""data"": [
                {{
                  ""localPluginPath"": ""{XPaths.JsonAbs("plugins/plug-1")}"",
                  ""plugin"": {{
                    ""id"": ""plug-1"",
                    ""remotePluginId"": ""remote-1"",
                    ""name"": ""Plugin One"",
                    ""installed"": true,
                    ""enabled"": true,
                    ""authPolicy"": ""ON_USE"",
                    ""installPolicy"": ""AVAILABLE"",
                    ""source"": {{ ""type"": ""remote"" }}
                  }}
                }}
              ]
            }}");
        var listRpc = new RecordingRpc { Result = listDoc.RootElement };
        await using var listClient = CreateClient(listRpc);

        var list = await listClient.ListPluginSharesAsync();

        list.Data.Should().ContainSingle();
        list.Data[0].Plugin.RemotePluginId.Should().Be("remote-1");
        list.Data[0].LocalPluginPath.Should().Be(XPaths.JsonAbs("plugins/plug-1"));
        listRpc.LastMethod.Should().Be("plugin/share/list");

        using var checkoutDoc = JsonDocument.Parse(
            $@"{{
              ""remotePluginId"": ""remote-1"",
              ""pluginId"": ""plug-1"",
              ""pluginName"": ""Plugin One"",
              ""pluginPath"": ""{XPaths.JsonAbs("plugins/plug-1")}"",
              ""marketplaceName"": ""workspace"",
              ""marketplacePath"": ""{XPaths.JsonAbs("market")}"",
              ""remoteVersion"": null
            }}");
        var checkoutRpc = new RecordingRpc { Result = checkoutDoc.RootElement };
        await using var checkoutClient = CreateClient(checkoutRpc);

        var checkout = await checkoutClient.CheckoutPluginShareAsync(new PluginShareCheckoutOptions { RemotePluginId = "remote-1" });

        checkout.PluginId.Should().Be("plug-1");
        checkout.RemoteVersion.Should().BeNull();
        checkoutRpc.LastMethod.Should().Be("plugin/share/checkout");
    }

    [Fact]
    public async Task ListPluginsAsync_MissingRequiredPluginFields_Throws()
    {
        var marketPath = XPaths.JsonAbs("market");
        using var doc = JsonDocument.Parse(
            $$"""
            {
              "marketplaces": [
                {
                  "name": "official",
                  "path": "{{marketPath}}",
                  "plugins": [
                    {
                      "id": "plug-1",
                      "name": "Plugin One",
                      "installed": true,
                      "enabled": true,
                      "authPolicy": "ON_INSTALL",
                      "installPolicy": "AVAILABLE"
                    }
                  ]
                }
              ]
            }
            """);
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var act = async () => await client.ListPluginsAsync(new PluginListOptions());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*source*");
    }

    [Fact]
    public async Task ReadPluginAsync_MissingMarketplaceName_Throws()
    {
        var marketPath = XPaths.JsonAbs("market");
        var pluginSourcePath = XPaths.JsonAbs("plugins/plug-1");
        using var doc = JsonDocument.Parse(
            $@"{{
              ""plugin"": {{
                ""marketplacePath"": ""{marketPath}"",
                ""skills"": [],
                ""apps"": [],
                ""mcpServers"": [],
                ""summary"": {{
                  ""id"": ""plug-1"",
                  ""name"": ""Plugin One"",
                  ""installed"": true,
                  ""enabled"": true,
                  ""authPolicy"": ""ON_USE"",
                  ""installPolicy"": ""INSTALLED_BY_DEFAULT"",
                  ""source"": {{
                    ""type"": ""local"",
                    ""path"": ""{pluginSourcePath}""
                  }}
                }}
              }}
            }}");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var act = async () => await client.ReadPluginAsync(new PluginReadOptions
        {
            MarketplacePath = XPaths.Abs("market"),
            PluginName = "plugin-one"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*marketplaceName*");
    }

    [Fact]
    public async Task InstallPluginAsync_MissingRequiredFields_Throws()
    {
        using var doc = JsonDocument.Parse("""{"appsNeedingAuth":[]}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var act = async () => await client.InstallPluginAsync(new PluginInstallOptions
        {
            MarketplacePath = XPaths.Abs("market"),
            PluginName = "plugin-one"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*authPolicy*");
    }

    [Fact]
    public async Task UninstallPluginAsync_SendsExpectedParams()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        await client.UninstallPluginAsync(new PluginUninstallOptions { PluginId = "plug-1", ForceRemoteSync = true });

        rpc.LastMethod.Should().Be("plugin/uninstall");
        var json = JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().Contain("\"pluginId\":\"plug-1\"");
        json.Should().NotContain("forceRemoteSync");
    }

    [Fact]
    public async Task UninstallPluginAsync_NonObjectResponse_Throws()
    {
        using var doc = JsonDocument.Parse("""null""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var act = async () => await client.UninstallPluginAsync(new PluginUninstallOptions { PluginId = "plug-1" });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*plugin/uninstall response must be a JSON object*");
    }

    private static CodexAppServerClient CreateClient(RecordingRpc rpc) =>
        new(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

    private sealed class FakeProcess : IStdioProcess
    {
        public Task Completion => Task.CompletedTask;
        public int? ProcessId => 1;
        public int? ExitCode => 0;
        public IReadOnlyList<string> StderrTail => Array.Empty<string>();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingRpc : IJsonRpcConnection
    {
        public string? LastMethod { get; private set; }
        public object? LastParams { get; private set; }
        public required JsonElement Result { get; init; }

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            LastMethod = method;
            LastParams = @params;
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ReadPluginAsync_RejectsRelativeMarketplacePath()
    {
        var rpc = new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement };
        await using var client = CreateClient(rpc);

        var act = async () => await client.ReadPluginAsync(new PluginReadOptions
        {
            MarketplacePath = "relative\\market",
            PluginName = "plugin-one"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*absolute path*");
        rpc.LastMethod.Should().BeNull();
    }

    [Fact]
    public async Task ListPluginsAsync_RejectsRelativeCwds()
    {
        var rpc = new RecordingRpc { Result = JsonDocument.Parse("""{}""").RootElement };
        await using var client = CreateClient(rpc);

        var act = async () => await client.ListPluginsAsync(new PluginListOptions
        {
            Cwds = ["relative\\repo"]
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*absolute paths*");
        rpc.LastMethod.Should().BeNull();
    }
}
