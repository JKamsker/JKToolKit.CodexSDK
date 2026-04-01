namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>skills/config/write</c>.
/// </summary>
public sealed class SkillsConfigWriteOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the selected skill should be enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the absolute path-based selector for the skill.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets the name-based selector for the skill.
    /// </summary>
    public string? Name { get; set; }
}
