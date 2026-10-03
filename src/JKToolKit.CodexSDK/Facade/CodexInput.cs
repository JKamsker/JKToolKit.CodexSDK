using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK;

/// <summary>Turn input with an explicit authority boundary.</summary>
public sealed class CodexInput
{
    private readonly string _text;
    private readonly string? _source;
    private CodexInput(string text, string? source) { _text = text; _source = source; }

    /// <summary>Creates user-authority text.</summary>
    public static CodexInput Text(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new(text, null);
    }

    /// <summary>Creates tool-authority content from another agent, tool, or application.</summary>
    /// <remarks>Uses standalone tool output, never user text or prompt delimiters. Cannot be mixed with user input in one turn.</remarks>
    public static CodexInput ExternalMessage(string source, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(text);
        return new(text, source);
    }

    internal TurnStartOptions ToOptions() => _source is null
        ? new() { Input = [TurnInputItem.Text(_text)] }
        : new() { ToolOutput = TurnToolOutput.Text(_source, _text) };
}
