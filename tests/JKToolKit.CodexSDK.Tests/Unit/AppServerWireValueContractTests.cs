using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerWireValueContractTests
{
    private delegate bool TryParser<T>(string? value, out T parsed);

    private static void AssertParseContract<T>(string value, Func<string,T> parse, TryParser<T> tryParse, Func<string,T> fromString, Func<T,string> toString)
        where T : struct
    {
        var parsed = parse(value);
        tryParse(value, out var tried).Should().BeTrue(); tried.Should().Be(parsed);
        fromString(value).Should().Be(parsed); toString(parsed).Should().Be(value); parsed.ToString().Should().Be(value);
        foreach (var invalid in new string?[] { null, "", " \t" })
        {
            Action fail = () => parse(invalid!);
            fail.Should().Throw<ArgumentException>().WithParameterName("value").WithMessage("*cannot be empty or whitespace*");
            tryParse(invalid, out var failed).Should().BeFalse(); failed.Should().Be(default(T));
        }
    }

    [Theory]
    [InlineData("FUTURE_VALUE")]
    [InlineData(" Future_Value ")]
    public void PluginValues_RoundTripUnknownWireValuesAndRejectEmptyInput(string wire)
    {
        AssertParseContract(wire, PluginAuthPolicy.Parse, PluginAuthPolicy.TryParse, s => (PluginAuthPolicy)s, v => (string)v);
        AssertParseContract(wire, PluginInstallPolicy.Parse, PluginInstallPolicy.TryParse, s => (PluginInstallPolicy)s, v => (string)v);
        AssertParseContract(wire, PluginSourceType.Parse, PluginSourceType.TryParse, s => (PluginSourceType)s, v => (string)v);
        AssertParseContract(wire, PluginAvailability.Parse, PluginAvailability.TryParse, s => (PluginAvailability)s, v => (string)v);
        AssertParseContract(wire, PluginDisabledReason.Parse, PluginDisabledReason.TryParse, s => (PluginDisabledReason)s, v => (string)v);
        AssertParseContract(wire, PluginSharePrincipalType.Parse, PluginSharePrincipalType.TryParse, s => (PluginSharePrincipalType)s, v => (string)v);
        AssertParseContract(wire, PluginShareTargetRole.Parse, PluginShareTargetRole.TryParse, s => (PluginShareTargetRole)s, v => (string)v);
        AssertParseContract(wire, PluginSharePrincipalRole.Parse, PluginSharePrincipalRole.TryParse, s => (PluginSharePrincipalRole)s, v => (string)v);
        AssertParseContract(wire, PluginShareDiscoverability.Parse, PluginShareDiscoverability.TryParse, s => (PluginShareDiscoverability)s, v => (string)v);
        AssertParseContract(wire, PluginShareUpdateDiscoverability.Parse, PluginShareUpdateDiscoverability.TryParse, s => (PluginShareUpdateDiscoverability)s, v => (string)v);
        AssertParseContract(wire, PluginListMarketplaceKind.Parse, PluginListMarketplaceKind.TryParse, s => (PluginListMarketplaceKind)s, v => (string)v);
        AssertParseContract(wire, CodexAuthMode.Parse, CodexAuthMode.TryParse, s => (CodexAuthMode)s, v => (string)v);
        AssertParseContract(wire, CodexPlanType.Parse, CodexPlanType.TryParse, s => (CodexPlanType)s, v => (string)v);
        AssertParseContract(wire, CliAuthCredentialsStoreMode.Parse, CliAuthCredentialsStoreMode.TryParse, s => (CliAuthCredentialsStoreMode)s, v => (string)v);
        AssertParseContract(wire, NetworkUnixSocketPermission.Parse, NetworkUnixSocketPermission.TryParse, s => (NetworkUnixSocketPermission)s, v => (string)v);
        AssertParseContract(wire, NetworkDomainPermission.Parse, NetworkDomainPermission.TryParse, s => (NetworkDomainPermission)s, v => (string)v);
        AssertParseContract(wire, ThreadSessionStartSource.Parse, ThreadSessionStartSource.TryParse, s => (ThreadSessionStartSource)s, v => (string)v);
        AssertParseContract(wire, TurnAdditionalContextKind.Parse, TurnAdditionalContextKind.TryParse, s => (TurnAdditionalContextKind)s, v => (string)v);
        AssertParseContract(wire, AllowDenyRequirementValue.Parse, AllowDenyRequirementValue.TryParse, s => (AllowDenyRequirementValue)s, v => (string)v);
        AssertParseContract(wire, BrowserUseAccessApprovalLifetimeValue.Parse, BrowserUseAccessApprovalLifetimeValue.TryParse, s => (BrowserUseAccessApprovalLifetimeValue)s, v => (string)v);
        AssertParseContract(wire, McpServerOauthClientRegistration.Parse, McpServerOauthClientRegistration.TryParse, s => (McpServerOauthClientRegistration)s, v => (string)v);
        AssertParseContract(wire, ModelMultiAgentVersion.Parse, ModelMultiAgentVersion.TryParse, s => (ModelMultiAgentVersion)s, v => (string)v);
        AssertParseContract(wire, PluginAppTemplateUnavailableReason.Parse, PluginAppTemplateUnavailableReason.TryParse, s => (PluginAppTemplateUnavailableReason)s, v => (string)v);
        AssertParseContract(wire, PluginInstallPolicySource.Parse, PluginInstallPolicySource.TryParse, s => (PluginInstallPolicySource)s, v => (string)v);
        AssertParseContract(wire, McpServerStartupFailureReason.Parse, McpServerStartupFailureReason.TryParse, s => (McpServerStartupFailureReason)s, v => (string)v);
        AssertParseContract(wire, PluginSearchScope.Parse, PluginSearchScope.TryParse, s => (PluginSearchScope)s, v => (string)v);
        AssertParseContract(wire, RemoteControlConnectionStatus.Parse, RemoteControlConnectionStatus.TryParse, s => (RemoteControlConnectionStatus)s, v => (string)v);
        AssertParseContract(wire, McpServerStatusDetail.Parse, McpServerStatusDetail.TryParse, s => (McpServerStatusDetail)s, v => (string)v);
        RemoteControlClientsListOrder order = wire;
        RemoteControlClientsListOrder.Parse(wire).Should().Be(order); ((string)order).Should().Be(wire); order.ToString().Should().Be(wire);
        foreach (var invalid in new string?[] { null, "", " " })
        {
            Action parse = () => RemoteControlClientsListOrder.Parse(invalid!);
            parse.Should().Throw<ArgumentException>().WithParameterName("value").WithMessage("*cannot be empty or whitespace*");
        }
        AssertParseContract(wire, SandboxNetworkAccess.Parse, SandboxNetworkAccess.TryParse, s => (SandboxNetworkAccess)s, v => (string)v);
    }

    [Fact]
    public void KnownPluginConstants_MatchTheirDocumentedWireSpellings()
    {
        new string[] { PluginAuthPolicy.OnInstall, PluginAuthPolicy.OnUse }.Should().Equal("ON_INSTALL", "ON_USE");
        new string[] { PluginInstallPolicy.NotAvailable, PluginInstallPolicy.Available, PluginInstallPolicy.InstalledByDefault }.Should().Equal("NOT_AVAILABLE", "AVAILABLE", "INSTALLED_BY_DEFAULT");
        new string[] { PluginSourceType.Local, PluginSourceType.Git, PluginSourceType.Remote }.Should().Equal("local", "git", "remote");
        new string[] { PluginAvailability.Available, PluginAvailability.DisabledByAdmin }.Should().Equal("AVAILABLE", "DISABLED_BY_ADMIN");
        new string[] { PluginDisabledReason.DisabledByAdmin, PluginDisabledReason.PlanNotEligible, PluginDisabledReason.RequiredAppUnavailable }.Should().Equal("disabled_by_admin", "plan_not_eligible", "required_app_unavailable");
        new string[] { PluginSharePrincipalType.User, PluginSharePrincipalType.Group, PluginSharePrincipalType.Workspace }.Should().Equal("user", "group", "workspace");
        new string[] { PluginShareTargetRole.Reader, PluginShareTargetRole.Editor }.Should().Equal("reader", "editor");
        new string[] { PluginSharePrincipalRole.Reader, PluginSharePrincipalRole.Editor, PluginSharePrincipalRole.Owner }.Should().Equal("reader", "editor", "owner");
        new string[] { PluginShareDiscoverability.Listed, PluginShareDiscoverability.Unlisted, PluginShareDiscoverability.Private }.Should().Equal("LISTED", "UNLISTED", "PRIVATE");
        new string[] { PluginShareUpdateDiscoverability.Unlisted, PluginShareUpdateDiscoverability.Private }.Should().Equal("UNLISTED", "PRIVATE");
        new string[] { PluginListMarketplaceKind.Local, PluginListMarketplaceKind.WorkspaceDirectory, PluginListMarketplaceKind.SharedWithMe }.Should().Equal("local", "workspace-directory", "shared-with-me");
        new string[] { CodexAuthMode.ApiKey, CodexAuthMode.ChatGpt, CodexAuthMode.ChatGptAuthTokens, CodexAuthMode.PersonalAccessToken }.Should().Equal("apikey", "chatgpt", "chatgptAuthTokens", "personalAccessToken");
        new string[] { CodexPlanType.Free, CodexPlanType.Go, CodexPlanType.Plus, CodexPlanType.Pro, CodexPlanType.ProMax, CodexPlanType.Team, CodexPlanType.SelfServeBusinessUsageBased, CodexPlanType.Business, CodexPlanType.EnterpriseCbpUsageBased, CodexPlanType.Enterprise, CodexPlanType.Edu, CodexPlanType.Unknown }
            .Should().Equal("free", "go", "plus", "pro", "promax", "team", "self_serve_business_usage_based", "business", "enterprise_cbp_usage_based", "enterprise", "edu", "unknown");
        new string[] { CliAuthCredentialsStoreMode.File, CliAuthCredentialsStoreMode.Keyring, CliAuthCredentialsStoreMode.Auto, CliAuthCredentialsStoreMode.Ephemeral }.Should().Equal("file", "keyring", "auto", "ephemeral");
        new string[] { NetworkUnixSocketPermission.Allow, NetworkUnixSocketPermission.Deny, NetworkUnixSocketPermission.None }.Should().Equal("allow", "deny", "none");
        new string[] { NetworkDomainPermission.Allow, NetworkDomainPermission.Deny }.Should().Equal("allow", "deny");
        new string[] { ThreadSessionStartSource.Startup, ThreadSessionStartSource.Clear }.Should().Equal("startup", "clear");
        new string[] { TurnAdditionalContextKind.Untrusted, TurnAdditionalContextKind.Application }.Should().Equal("untrusted", "application");
        new string[] { AllowDenyRequirementValue.Allow, AllowDenyRequirementValue.Deny }.Should().Equal("allow", "deny");
        new string[] { BrowserUseAccessApprovalLifetimeValue.Turn, BrowserUseAccessApprovalLifetimeValue.Thread }.Should().Equal("turn", "thread");
        new string[] { McpServerOauthClientRegistration.Auto, McpServerOauthClientRegistration.Cimd, McpServerOauthClientRegistration.Dcr }.Should().Equal("auto", "cimd", "dcr");
        new string[] { ModelMultiAgentVersion.Disabled, ModelMultiAgentVersion.V1, ModelMultiAgentVersion.V2 }.Should().Equal("disabled", "v1", "v2");
        default(McpServerOauthClientRegistration).Value.Should().BeEmpty(); default(ModelMultiAgentVersion).Value.Should().BeEmpty();
        PluginAvailability.TryParse("eNaBlEd", out var alias).Should().BeTrue(); alias.Should().Be(PluginAvailability.Available);
        new string[] { PluginAppTemplateUnavailableReason.NotConfiguredForWorkspace, PluginAppTemplateUnavailableReason.NoActiveWorkspace }.Should().Equal("NOT_CONFIGURED_FOR_WORKSPACE", "NO_ACTIVE_WORKSPACE");
        new string[] { PluginInstallPolicySource.WorkspaceSetting, PluginInstallPolicySource.ImplicitCanonicalApp }.Should().Equal("WORKSPACE_SETTING", "IMPLICIT_CANONICAL_APP");
        ((string)McpServerStartupFailureReason.ReauthenticationRequired).Should().Be("reauthenticationRequired");
        default(McpServerStartupFailureReason).Value.Should().BeEmpty(); default(PluginInstallPolicySource).Value.Should().BeEmpty();
        new string[] { PluginSearchScope.Global, PluginSearchScope.Workspace, PluginSearchScope.Personal }.Should().Equal("global", "workspace", "personal");
        new string[] { RemoteControlConnectionStatus.Disabled, RemoteControlConnectionStatus.Connecting, RemoteControlConnectionStatus.Connected, RemoteControlConnectionStatus.Errored }.Should().Equal("disabled", "connecting", "connected", "errored");
        new string[] { RemoteControlClientsListOrder.Asc, RemoteControlClientsListOrder.Desc }.Should().Equal("asc", "desc");
        new string[] { McpServerStatusDetail.Full, McpServerStatusDetail.ToolsAndAuthOnly }.Should().Equal("full", "toolsAndAuthOnly");
        new string[] { SandboxNetworkAccess.Restricted, SandboxNetworkAccess.Enabled }.Should().Equal("restricted", "enabled");
    }

    private static JsonSerializerOptions NetworkOptions => new() { Converters = { new SandboxNetworkAccessJsonConverter() } };

    [Theory]
    [InlineData("restricted")]
    [InlineData("enabled")]
    [InlineData("future")]
    public void SandboxNetworkAccess_UsesStringWireRepresentation(string value)
    {
        var json = JsonSerializer.Serialize(value);
        var parsed = JsonSerializer.Deserialize<SandboxNetworkAccess>(json, NetworkOptions);
        parsed.Value.Should().Be(value); JsonSerializer.Serialize(parsed, NetworkOptions).Should().Be(json);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("\" \"")]
    public void SandboxNetworkAccess_RejectsInvalidJsonAndDefaultValues(string json)
    {
        Action read = () => JsonSerializer.Deserialize<SandboxNetworkAccess>(json, NetworkOptions);
        Action write = () => JsonSerializer.Serialize(default(SandboxNetworkAccess), NetworkOptions);
        read.Should().Throw<JsonException>().WithMessage(json == "\" \"" ? "*cannot be null or whitespace*" : "*must be a JSON string*");
        write.Should().Throw<JsonException>().WithMessage("*cannot be empty or whitespace*");
    }

    [Theory]
    [InlineData("form", McpServerElicitationMode.Form)]
    [InlineData("url", McpServerElicitationMode.Url)]
    public void ElicitationMode_RoundTripsKnownWireNames(string wire, McpServerElicitationMode mode)
    {
        JsonSerializer.Deserialize<McpServerElicitationMode>(JsonSerializer.Serialize(wire)).Should().Be(mode);
        JsonSerializer.Serialize(mode).Should().Be(JsonSerializer.Serialize(wire));
    }

    [Theory]
    [InlineData("accept", McpServerElicitationAction.Accept)]
    [InlineData("decline", McpServerElicitationAction.Decline)]
    [InlineData("cancel", McpServerElicitationAction.Cancel)]
    public void ElicitationAction_RoundTripsKnownWireNames(string wire, McpServerElicitationAction action)
    {
        JsonSerializer.Deserialize<McpServerElicitationAction>(JsonSerializer.Serialize(wire)).Should().Be(action);
        JsonSerializer.Serialize(action).Should().Be(JsonSerializer.Serialize(wire));
    }

    [Theory]
    [InlineData("\"future\"")]
    [InlineData("null")]
    [InlineData("42")]
    public void ElicitationConverters_RejectUnknownAndNonStringValues(string json)
    {
        Action mode = () => JsonSerializer.Deserialize<McpServerElicitationMode>(json);
        Action action = () => JsonSerializer.Deserialize<McpServerElicitationAction>(json);
        Action writeMode = () => JsonSerializer.Serialize((McpServerElicitationMode)42);
        Action writeAction = () => JsonSerializer.Serialize((McpServerElicitationAction)42);
        mode.Should().Throw<JsonException>(); action.Should().Throw<JsonException>();
        if (json != "42")
        {
            mode.Should().Throw<JsonException>().WithMessage("*Unknown MCP elicitation mode*");
            action.Should().Throw<JsonException>().WithMessage("*Unknown MCP elicitation action*");
        }
        writeMode.Should().Throw<JsonException>().WithMessage("*Unknown MCP elicitation mode '42'*");
        writeAction.Should().Throw<JsonException>().WithMessage("*Unknown MCP elicitation action '42'*");
    }

    [Fact]
    public void ElicitationRequestAndResponse_UseProtocolPropertyNamesAndPreserveStructuredContent()
    {
        const string json = """{"threadId":"thread","turnId":"turn","serverName":"server","mode":"url","message":"Open URL","requestedSchema":{"type":"object"},"url":"https://auth.test","elicitationId":"e","_meta":{"trace":"t"}}""";
        var request = JsonSerializer.Deserialize<McpServerElicitationRequestParams>(json)!;
        request.ThreadId.Should().Be("thread"); request.TurnId.Should().Be("turn"); request.ServerName.Should().Be("server"); request.Mode.Should().Be(McpServerElicitationMode.Url);
        request.Message.Should().Be("Open URL"); request.RequestedSchema!.Value.GetProperty("type").GetString().Should().Be("object");
        request.Url.Should().Be("https://auth.test"); request.ElicitationId.Should().Be("e"); request.Meta!.Value.GetProperty("trace").GetString().Should().Be("t");
        JsonElement.DeepEquals(JsonSerializer.SerializeToElement(request), JsonSerializer.Deserialize<JsonElement>(json)).Should().BeTrue();
        const string responseJson = """{"action":"accept","content":{"answer":"yes"},"_meta":{"trace":"t"}}""";
        var response = JsonSerializer.Deserialize<McpServerElicitationRequestResponse>(responseJson)!;
        response.Action.Should().Be(McpServerElicitationAction.Accept); response.Content!.Value.GetProperty("answer").GetString().Should().Be("yes"); response.Meta!.Value.GetProperty("trace").GetString().Should().Be("t");
        JsonElement.DeepEquals(JsonSerializer.SerializeToElement(response), JsonSerializer.Deserialize<JsonElement>(responseJson)).Should().BeTrue();
    }
}
