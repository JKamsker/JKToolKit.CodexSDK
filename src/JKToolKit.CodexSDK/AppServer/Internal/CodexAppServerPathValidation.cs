using System.IO;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static class CodexAppServerPathValidation
{
    public static void ValidateRequiredAbsolutePath(string? path, string paramName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"{displayName} cannot be empty or whitespace.", paramName);
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"{displayName} must be an absolute path.", paramName);
        }
    }

    public static void ValidateOptionalAbsolutePaths(IReadOnlyList<string>? paths, string paramName, string displayName)
    {
        if (paths is null)
        {
            return;
        }

        for (var i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException($"{displayName} entries cannot be empty or whitespace.", paramName);
            }

            if (!Path.IsPathFullyQualified(path))
            {
                throw new ArgumentException($"{displayName} entries must be absolute paths.", paramName);
            }
        }
    }

    public static string RequireAbsolutePayloadPath(string? path, string propertyName, string context)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"Missing required absolute path '{propertyName}' on {context}.");
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException($"Property '{propertyName}' on {context} must be an absolute path.");
        }

        return path;
    }

    public static string? GetOptionalAbsolutePayloadPath(string? path, string propertyName, string context)
    {
        if (path is null)
        {
            return null;
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException($"Property '{propertyName}' on {context} must be an absolute path.");
        }

        return path;
    }

    public static IReadOnlyList<string> GetOptionalAbsolutePayloadPaths(
        IReadOnlyList<string>? paths,
        string propertyName,
        string context)
    {
        if (paths is null)
        {
            return Array.Empty<string>();
        }

        if (paths.Count == 0)
        {
            return Array.Empty<string>();
        }

        var validated = new List<string>(paths.Count);
        foreach (var path in paths)
        {
            validated.Add(RequireAbsolutePayloadPath(path, propertyName, context));
        }

        return validated;
    }
}
