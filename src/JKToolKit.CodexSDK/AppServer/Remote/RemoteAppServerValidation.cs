namespace JKToolKit.CodexSDK.AppServer;

internal static class RemoteAppServerValidation
{
    public static void ValidateSshStart(CodexSshWebSocketAppServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Host);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.CodexExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SshExecutable);
        if (options.Password is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(options.SshpassExecutable);
        }

        if (options.Port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "SSH port must be between 1 and 65535.");
        }
    }

    public static void ValidateDockerContainerStart(CodexDockerContainerWebSocketAppServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Image);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DockerExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.CodexExecutable);
        ValidatePort(options.ContainerPort, "Container port must be between 1 and 65535.");
    }

    public static void ValidateDockerExecStart(CodexDockerExecWebSocketAppServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Container);
        ArgumentNullException.ThrowIfNull(options.PublicUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DockerExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.CodexExecutable);
        if (options.PublicUri.Scheme is not "ws" and not "wss")
        {
            throw new ArgumentException("PublicUri must use ws:// or wss://.", nameof(options));
        }

        ValidatePort(options.ContainerPort, "Container port must be between 1 and 65535.");
    }

    private static void ValidatePort(int port, string message)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), message);
        }
    }
}
