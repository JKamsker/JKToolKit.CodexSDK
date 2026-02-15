using System.Text.Json;

namespace JKToolKit.CodexSDK.Tests.TestHelpers;

internal static class JsonFixtures
{
    internal static string LoadText(string name)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Fixture '{name}' was not found in the test output directory.", fullPath);
        }

        return File.ReadAllText(fullPath);
    }

    internal static JsonElement Load(string name)
    {
        using var doc = JsonDocument.Parse(LoadText(name));
        return doc.RootElement.Clone();
    }
}
