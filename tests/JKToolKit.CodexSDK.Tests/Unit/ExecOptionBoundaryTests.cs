using System.Text.Json;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.StructuredOutputs;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExecOptionBoundaryTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Timeouts_RejectNonPositiveWithoutChangingPriorValue(int milliseconds)
    {
        var options = new CodexClientOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.StartTimeout = TimeSpan.FromMilliseconds(milliseconds));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.ProcessExitTimeout = TimeSpan.FromMilliseconds(milliseconds));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.TailPollInterval = TimeSpan.FromMilliseconds(milliseconds));
        Assert.Equal(TimeSpan.FromSeconds(30), options.StartTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ProcessExitTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(200), options.TailPollInterval);
    }

    [Fact]
    public void PollInterval_AcceptsExactMinimumAndValidatesExecutableExistence()
    {
        var options = new CodexClientOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.TailPollInterval = TimeSpan.FromMilliseconds(50) - TimeSpan.FromTicks(1));
        options.TailPollInterval = TimeSpan.FromMilliseconds(50);
        options.Validate();
        options.CodexExecutablePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var ex = Assert.Throws<FileNotFoundException>(options.Validate);
        Assert.Equal(options.CodexExecutablePath, ex.FileName);
        var file = Path.GetTempFileName();
        try { options.CodexExecutablePath = file; options.Validate(); }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Session_RequiredFieldsAndPromptModesAreValidated()
    {
        var options = new CodexSessionOptions();
        Assert.Throws<InvalidOperationException>(() => options.WorkingDirectory);
        Assert.Throws<InvalidOperationException>(() => options.Prompt);
        Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Throws<ArgumentException>(() => options.WorkingDirectory = " ");
        Assert.Throws<ArgumentException>(() => options.Prompt = " ");
        Assert.Throws<ArgumentException>(() => options.PromptArgument = " ");
        options.WorkingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Assert.Throws<InvalidOperationException>(options.Validate);
        options.WorkingDirectory = Path.GetTempPath();
        Assert.Throws<InvalidOperationException>(options.Validate);
        options.PromptArgument = "argument";
        options.StdinPayload = "payload";
        options.Validate();
        Assert.Equal("argument", options.CommandPromptToken);
        Assert.Equal("payload", options.StandardInputPayload);
        Assert.Null(options.ResumeStandardInputPayload);
        var clone = options.Clone();
        Assert.Equal("argument", clone.PromptArgument);
        Assert.Equal("payload", clone.StdinPayload);
        options.PromptArgument = null;
        options.Prompt = "legacy";
        Assert.Throws<InvalidOperationException>(options.Validate);
        options.StdinPayload = null;
        options.Validate();
        Assert.Equal("-", options.CommandPromptToken);
        Assert.Equal("legacy", options.ResumeStandardInputPayload);
        options.Model = default;
        Assert.Throws<InvalidOperationException>(options.Validate);
        options.Model = CodexModel.Gpt52Codex;
        options.ReasoningEffort = default;
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Theory]
    [InlineData("--thread-source")]
    [InlineData("--THREAD-SOURCE=cli")]
    public void ThreadSource_RejectsDuplicateAdditionalArguments(string argument)
    {
        var options = new CodexSessionOptions(Path.GetTempPath(), "prompt") { ThreadSource = "sdk", AdditionalOptions = [" ", "--json", argument] };
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Theory]
    [InlineData("--output-schema")]
    [InlineData("--OUTPUT-SCHEMA=other.json")]
    public void OutputSchema_RejectsDuplicateAdditionalArguments(string argument)
    {
        var options = new CodexSessionOptions(Path.GetTempPath(), "prompt") { OutputSchema = CodexOutputSchema.FromJson(JsonSerializer.SerializeToElement(new { type = "object" })), AdditionalOptions = [" ", "--json", argument] };
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Theory]
    [InlineData("[]", "JSON object")]
    [InlineData("{broken", "not valid JSON")]
    public void SchemaFile_RejectsInvalidRootAndSyntax(string json, string message)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            var options = new CodexSessionOptions(Path.GetTempPath(), "prompt") { OutputSchema = CodexOutputSchema.FromFile(path) };
            Assert.Contains(message, Assert.Throws<InvalidOperationException>(options.Validate).Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Schema_ValidFileAccepted_MissingFileAndScalarRejected()
    {
        var path = Path.GetTempFileName();
        var options = new CodexSessionOptions(Path.GetTempPath(), "prompt") { OutputSchema = CodexOutputSchema.FromFile(path) };
        try { File.WriteAllText(path, "{}"); options.Validate(); }
        finally { File.Delete(path); }
        Assert.Contains("does not exist", Assert.Throws<InvalidOperationException>(options.Validate).Message);
        options.OutputSchema = CodexOutputSchema.FromJson(JsonSerializer.SerializeToElement(4));
        Assert.Contains("JSON object", Assert.Throws<InvalidOperationException>(options.Validate).Message);
    }

    [Fact]
    public void Session_SettersRejectInvalidOptionalValues()
    {
        var options = new CodexSessionOptions(Path.GetTempPath(), "prompt");
        Assert.Throws<ArgumentException>(() => options.ThreadSource = " ");
        Assert.Throws<ArgumentOutOfRangeException>(() => options.IdleTimeout = TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.IdleTimeout = TimeSpan.FromTicks(-1));
    }

    [Fact]
    public void Review_ClonePreservesTargetAndCopiesAdditionalOptions()
    {
        var extras = new List<string> { "--json" };
        var options = new CodexReviewOptions(Path.GetTempPath()) { CommitSha = "abc", Title = "title", AdditionalOptions = extras, CodexBinaryPath = "binary" };
        var clone = options.Clone();
        extras.Add("--new");
        Assert.Equal("abc", clone.CommitSha);
        Assert.Equal("title", clone.Title);
        Assert.Equal("binary", clone.CodexBinaryPath);
        Assert.Equal(new[] { "--json" }, clone.AdditionalOptions);
        clone.CommitSha = "def";
        Assert.Equal("abc", options.CommitSha);
    }

    [Fact]
    public void Review_MissingDirectoryAndWhitespaceInputsAreRejected()
    {
        var options = new CodexReviewOptions();
        Assert.Throws<InvalidOperationException>(() => options.WorkingDirectory);
        Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Throws<ArgumentException>(() => options.WorkingDirectory = " ");
        Assert.Throws<ArgumentException>(() => options.Prompt = " ");
        Assert.Throws<ArgumentException>(() => options.CommitSha = " ");
        Assert.Throws<ArgumentException>(() => options.BaseBranch = " ");
        Assert.Throws<ArgumentException>(() => options.Title = " ");
        options.WorkingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void StreamOptions_PreserveTimestampAndValidateOffsetBoundaries()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var options = EventStreamOptions.FromTimestamp(timestamp, false);
        Assert.False(options.FromBeginning);
        Assert.False(options.Follow);
        Assert.Equal(timestamp, options.AfterTimestamp);
        Assert.Null(options.FromByteOffset);
        Assert.Equal("byteOffset", Assert.Throws<ArgumentOutOfRangeException>(() => EventStreamOptions.FromOffset(-1)).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventStreamOptions(FromByteOffset: -1));
        Assert.Equal(0, new EventStreamOptions(FromByteOffset: 0).FromByteOffset);
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventStreamOptions { FromByteOffset = -1 });
        Assert.Equal(0, (options with { FromByteOffset = 0 }).FromByteOffset);
        Assert.Null((options with { FromByteOffset = null }).FromByteOffset);
    }

    [Fact]
    public void FilterFactoriesAndDateBounds_PreserveCriteria()
    {
        var date = DateTimeOffset.UtcNow;
        Assert.Equal(date, SessionFilter.ForDateRange(date, date).ToDate);
        Assert.Throws<ArgumentException>(() => new SessionFilter(FromDate: date, ToDate: date.AddTicks(-1)));
        Assert.Equal("toDate", Assert.Throws<ArgumentException>(() => SessionFilter.ForDateRange(date, date.AddTicks(-1))).ParamName);
        Assert.Equal(date.AddTicks(1), SessionFilter.ForDateRange(date, date.AddTicks(1)).ToDate);
        Assert.Equal(date, (new SessionFilter { FromDate = date, ToDate = date }).ToDate);
        Assert.Null((new SessionFilter { FromDate = date, ToDate = null }).ToDate);
        Assert.Equal(date, (new SessionFilter { ToDate = date }).ToDate);
        Assert.Null(SessionFilter.None.FromDate);
        Assert.Equal("model", SessionFilter.ForModel("model").Model!.Value.Value);
        Assert.Equal("/repo", SessionFilter.ForWorkingDirectory("/repo").WorkingDirectory);
        Assert.Equal("abc*", SessionFilter.ForSessionIdPattern("abc*").SessionIdPattern);
        Assert.Throws<ArgumentNullException>(() => SessionFilter.ForSessionIdPattern(null!));
        Assert.Throws<ArgumentNullException>(() => SessionFilter.ForWorkingDirectory(null!));
    }

    [Fact]
    public void LaunchAndResumeHelpers_PreserveCopiesAndDescriptions()
    {
        Assert.Throws<ArgumentException>(() => CodexLaunch.FromFileName(" "));
        Assert.Throws<ArgumentException>(() => CodexLaunch.CodexOnPath().WithEnvironment(" ", "value"));
        Assert.Throws<ArgumentException>(() => CodexResumeTarget.BySelector(" "));
        var launch = CodexLaunch.FromFileName("launcher");
        Assert.Same(launch, launch.WithArgs());
        Assert.Equal("/repo", launch.WithWorkingDirectory("/repo").WorkingDirectory);
        Assert.Null(launch.WorkingDirectory);
        Assert.Equal("--last", CodexResumeTarget.MostRecent().Description);
        Assert.Equal("--last --all", CodexResumeTarget.MostRecent(true).Description);
        Assert.Equal("named", CodexResumeTarget.BySelector("named").Description);
        Assert.Equal("named --all", CodexResumeTarget.BySelector("named", true).Description);
        CodexResumeTarget.MostRecent().Validate();
        CodexResumeTarget.BySelector("named").Validate();
    }
}
