using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

using static CodexThreadItemParserJson;

internal static class CodexAgentMessageVersionedDisplayParser
{
    public static bool TryParse(JsonElement instruction, out CodexAgentMessageVersionedDisplay? display)
    {
        display = null;
        if (!TryGetOptionalInt32(instruction, "version", out var version) || version != 1 ||
            !TryGetRequiredString(instruction, "mode", out var mode))
            return false;

        switch (mode)
        {
            case "show":
                if (!TryGetRequiredString(instruction, "trustedContacts", out var contacts) ||
                    contacts is not ("check" or "skip") ||
                    !TryGetOptionalObject(instruction, "annotation", out var annotation))
                    return false;

                CodexDisplayHelplineSubject? subject = null;
                if (annotation is { } annotationValue)
                {
                    if (!TryGetRequiredString(annotationValue, "kind", out var kind) || kind != "wellbeingHelpline" ||
                        !TryGetRequiredString(annotationValue, "subject", out var subjectValue) ||
                        subjectValue is not ("answer" or "prompt"))
                        return false;
                    subject = subjectValue == "answer" ? CodexDisplayHelplineSubject.Answer : CodexDisplayHelplineSubject.Prompt;
                }
                display = new CodexAgentMessageDisplayShow(
                    contacts == "check" ? CodexDisplayTrustedContacts.Check : CodexDisplayTrustedContacts.Skip, subject);
                return true;

            case "hide":
                if (!TryGetRequiredObject(instruction, "replacement", out var replacement) ||
                    !TryGetRequiredString(replacement, "kind", out var replacementKind) || replacementKind != "wellbeingPromptHelpline")
                    return false;
                display = new CodexAgentMessageDisplayHide();
                return true;

            case "block":
                if (!TryGetRequiredObject(instruction, "reason", out var reason) ||
                    !TryGetRequiredString(reason, "kind", out var reasonKind))
                    return false;
                if (reasonKind == "protection" && TryGetRequiredString(reason, "protectionType", out var protectionType))
                    display = new CodexAgentMessageDisplayProtection(protectionType);
                else if (reasonKind == "cyber" && TryGetOptionalBool(reason, "trustedAccess", out var trustedAccess))
                    display = new CodexAgentMessageDisplayCyber(trustedAccess);
                return display is not null;

            default:
                return false;
        }
    }
}
