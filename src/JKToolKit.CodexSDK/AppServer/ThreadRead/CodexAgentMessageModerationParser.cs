using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

using static CodexThreadItemParserJson;

internal static class CodexAgentMessageModerationParser
{
    public static bool TryParse(JsonElement element, out CodexAgentMessageModeration? moderation)
    {
        moderation = null;
        if (!element.TryGetProperty("moderation", out var value) || value.ValueKind == JsonValueKind.Null)
            return true;

        if (value.ValueKind != JsonValueKind.Object ||
            !TryGetOptionalBool(value, "moderated", out var moderated) || moderated is null)
            return false;

        CodexAgentMessageDisplay? display = null;
        if (value.TryGetProperty("display", out var instruction) && instruction.ValueKind != JsonValueKind.Null)
        {
            if (instruction.ValueKind != JsonValueKind.Object ||
                !TryGetRequiredString(instruction, "action", out var action) ||
                !TryGetOptionalString(instruction, "blockType", out var blockType) ||
                !TryGetOptionalBool(instruction, "cyberTrustedAccess", out var cyberTrustedAccess) ||
                !TryGetOptionalBool(instruction, "checkTrustedContacts", out var checkTrustedContacts))
                return false;

            display = new CodexAgentMessageDisplay
            {
                Action = action,
                BlockType = blockType,
                CyberTrustedAccess = cyberTrustedAccess,
                CheckTrustedContacts = checkTrustedContacts
            };
        }

        moderation = new CodexAgentMessageModeration { Moderated = moderated.Value, Display = display };
        return true;
    }
}
