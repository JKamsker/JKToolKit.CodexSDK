using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerExperimentalGuardContractTests
{
    [Fact]
    public void ExperimentalOptions_RequireCapabilityEvenForExplicitEmptyCollectionsOrFalseFlags()
    {
        var cases = new (string Descriptor, Action<bool> Validate)[]
        {
            ("thread/start.runtimeWorkspaceRoots", enabled => ExperimentalApiGuards.ValidateThreadStart(new() { RuntimeWorkspaceRoots = [] }, enabled)),
            ("thread/start.environments", enabled => ExperimentalApiGuards.ValidateThreadStart(new() { Environments = [] }, enabled)),
            ("thread/start.daybreakEnabled", enabled => ExperimentalApiGuards.ValidateThreadStart(new() { DaybreakEnabled = false }, enabled)),
            ("thread/start.permissions", enabled => ExperimentalApiGuards.ValidateThreadStart(new() { PermissionProfileId = "profile" }, enabled)),
            ("thread/resume.runtimeWorkspaceRoots", enabled => ExperimentalApiGuards.ValidateThreadResume(new() { ThreadId = "t", RuntimeWorkspaceRoots = [] }, enabled)),
            ("thread/resume.permissions", enabled => ExperimentalApiGuards.ValidateThreadResume(new() { ThreadId = "t", PermissionProfileId = "profile" }, enabled)),
            ("askForApproval.granular", enabled => ExperimentalApiGuards.ValidateThreadFork(new() { ThreadId = "t", AskForApproval = new CodexAskForApprovalGranular { SandboxApproval = false, Rules = false, McpElicitations = false } }, enabled)),
            ("thread/fork.runtimeWorkspaceRoots", enabled => ExperimentalApiGuards.ValidateThreadFork(new() { ThreadId = "t", RuntimeWorkspaceRoots = [] }, enabled)),
            ("thread/fork.permissions", enabled => ExperimentalApiGuards.ValidateThreadFork(new() { ThreadId = "t", PermissionProfileId = "profile" }, enabled)),
            ("turn/start.runtimeWorkspaceRoots", enabled => ExperimentalApiGuards.ValidateTurnStart(new() { RuntimeWorkspaceRoots = [] }, enabled)),
            ("turn/start.environments", enabled => ExperimentalApiGuards.ValidateTurnStart(new() { Environments = [] }, enabled)),
            ("turn/start.permissions", enabled => ExperimentalApiGuards.ValidateTurnStart(new() { PermissionProfileId = "profile" }, enabled)),
            ("turn/steer.responsesapiClientMetadata", enabled => ExperimentalApiGuards.ValidateTurnSteer(new() { ThreadId = "t", ExpectedTurnId = "u", Input = [], ResponsesApiClientMetadata = new Dictionary<string,string>() }, enabled))
        };
        foreach (var (descriptor, validate) in cases)
        {
            Action disabled = () => validate(false); Action enabled = () => validate(true);
            disabled.Should().Throw<CodexExperimentalApiRequiredException>().Which.Descriptor.Should().Be(descriptor);
            enabled.Should().NotThrow(descriptor + " is supported with the capability enabled");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConflictingPermissionSettings_AreRejectedRegardlessOfCapability(bool enabled)
    {
        var cases = new Action[]
        {
            () => ExperimentalApiGuards.ValidateThreadStart(new() { Sandbox = CodexSandboxMode.ReadOnly, PermissionProfileId = "profile" }, enabled),
            () => ExperimentalApiGuards.ValidateThreadResume(new() { ThreadId = "t", Sandbox = CodexSandboxMode.ReadOnly, PermissionProfileId = "profile" }, enabled),
            () => ExperimentalApiGuards.ValidateThreadFork(new() { ThreadId = "t", Sandbox = CodexSandboxMode.ReadOnly, PermissionProfileId = "profile" }, enabled),
            () => ExperimentalApiGuards.ValidateTurnStart(new() { SandboxPolicy = new SandboxPolicy.ReadOnly(), PermissionProfileId = "profile" }, enabled)
        };
        foreach (var validate in cases)
            validate.Should().Throw<ArgumentException>().WithParameterName("options").WithMessage("*Sandbox*PermissionProfileId cannot both be set*");
        foreach (var blank in new[] { "", " \t" })
        {
            Action project = () => ExperimentalApiGuards.ValidateThreadStart(new() { ProjectId = blank }, enabled);
            project.Should().Throw<ArgumentException>().WithParameterName("options").WithMessage("*ProjectId cannot be empty or whitespace*");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void AbsentPermissionProfile_DoesNotConflictOrRequireExperimentalCapability(string? profile)
    {
        ExperimentalApiGuards.ValidateThreadStart(new() { Sandbox = CodexSandboxMode.ReadOnly, PermissionProfileId = profile }, false);
        ExperimentalApiGuards.ValidateThreadResume(new() { ThreadId = "t", Sandbox = CodexSandboxMode.ReadOnly, PermissionProfileId = profile, Path = " " }, false);
        ExperimentalApiGuards.ValidateThreadFork(new() { ThreadId = "t", Sandbox = CodexSandboxMode.ReadOnly, PermissionProfileId = profile, Path = " " }, false);
        ExperimentalApiGuards.ValidateTurnStart(new() { SandboxPolicy = new SandboxPolicy.ReadOnly(), PermissionProfileId = profile }, false);
        ExperimentalApiGuards.ValidateTurnSteer(new() { ThreadId = "t", ExpectedTurnId = "u", Input = [] }, false);
    }
    [Fact]
    public void ThreadFork_ExplainsWhichIdentifiersAreRequired()
    {
        Action fork = () => ExperimentalApiGuards.ValidateThreadFork(new() { ThreadId = " ", Path = " " }, true);
        fork.Should().Throw<ArgumentException>().WithParameterName("options").WithMessage("*Either ThreadId or Path must be specified*");
    }

}
