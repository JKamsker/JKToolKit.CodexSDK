using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerCollaborationMode;

public sealed class AppServerCollaborationModeSettings : AppServerWithPromptSettingsBase
{
    public AppServerCollaborationModeSettings()
    {
        Prompt = "Say 'ok' and nothing else.";
    }
}
