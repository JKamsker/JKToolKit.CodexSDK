namespace JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

/// <summary>
/// V2 <c>UserInput</c> DTO used by <c>turn/start</c>.
/// </summary>
public interface IUserInput
{
    /// <summary>
    /// Gets the wire discriminator for the user input type.
    /// </summary>
    string Type { get; }
}
