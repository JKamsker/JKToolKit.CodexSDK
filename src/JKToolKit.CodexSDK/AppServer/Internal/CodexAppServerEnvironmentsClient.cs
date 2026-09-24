using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerEnvironmentsClient
{
    private readonly Func<string, object?, CancellationToken, Task<JsonElement>> _sendRequestAsync;
    private readonly Func<bool> _experimentalApiEnabled;

    public CodexAppServerEnvironmentsClient(
        Func<string, object?, CancellationToken, Task<JsonElement>> sendRequestAsync,
        Func<bool> experimentalApiEnabled)
    {
        _sendRequestAsync = sendRequestAsync ?? throw new ArgumentNullException(nameof(sendRequestAsync));
        _experimentalApiEnabled = experimentalApiEnabled ?? throw new ArgumentNullException(nameof(experimentalApiEnabled));
    }

    public async Task<EnvironmentAddResult> AddAsync(EnvironmentAddOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException(AppServerMethods.EnvironmentAdd);
        }

        ValidateRequiredString(options.EnvironmentId, "EnvironmentId", nameof(options));
        ValidateRequiredString(options.ExecServerUrl, "ExecServerUrl", nameof(options));

        var result = await _sendRequestAsync(
            AppServerMethods.EnvironmentAdd,
            new
            {
                environmentId = options.EnvironmentId,
                execServerUrl = options.ExecServerUrl
            },
            ct);

        if (result.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("environment/add response must be a JSON object.");
        }

        return new EnvironmentAddResult { Raw = result };
    }

    private static void ValidateRequiredString(string? value, string displayName, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} cannot be empty or whitespace.", paramName);
        }
    }
}
