using JKToolKit.CodexSDK.Demo.Commands.AppServerApproval;
using JKToolKit.CodexSDK.Demo.Commands.AppServerConfig;
using JKToolKit.CodexSDK.Demo.Commands.AppServerFuzzyFileSearch;
using JKToolKit.CodexSDK.Demo.Commands.AppServerMcpManagement;
using JKToolKit.CodexSDK.Demo.Commands.AppServerNotifications;
using JKToolKit.CodexSDK.Demo.Commands.AppServerResilientStream;
using JKToolKit.CodexSDK.Demo.Commands.AppServerReview;
using JKToolKit.CodexSDK.Demo.Commands.AppServerSkillsApps;
using JKToolKit.CodexSDK.Demo.Commands.AppServerStream;
using JKToolKit.CodexSDK.Demo.Commands.AppServerTurnControl;
using JKToolKit.CodexSDK.Demo.Commands.DiOverrides;
using JKToolKit.CodexSDK.Demo.Commands.AppServerThreads;
using JKToolKit.CodexSDK.Demo.Commands.Exec;
using JKToolKit.CodexSDK.Demo.Commands.McpServer;
using JKToolKit.CodexSDK.Demo.Commands.Review;
using JKToolKit.CodexSDK.Demo.Commands.StructuredReview;
using Spectre.Console.Cli;

namespace JKToolKit.CodexSDK.Demo;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var app = new CommandApp();

        app.Configure(config =>
        {
            config.SetApplicationName("JKToolKit.CodexSDK.Demo");

            config.AddCommand<ExecCommand>("exec")
                .WithDescription("Start/resume an Exec-mode session and stream events.");

            config.AddCommand<ExecListSessionsCommand>("exec-list")
                .WithDescription("List Exec-mode sessions from the sessions directory.");

            config.AddCommand<ExecAttachCommand>("exec-attach")
                .WithDescription("Attach to an existing Exec-mode JSONL log and stream events.");

            config.AddCommand<ReviewCommand>("review")
                .WithDescription("Run a non-interactive `codex review` and print stdout/stderr.");

            config.AddCommand<AppServerStreamCommand>("appserver-stream")
                .WithDescription("Start `codex app-server` and stream turn output.");

            config.AddCommand<AppServerResilientStreamCommand>("appserver-resilient-stream")
                .WithDescription("Start `codex app-server` with auto-restart enabled and stream turn output.");

            config.AddCommand<AppServerApprovalCommand>("appserver-approval")
                .WithDescription("Start `codex app-server` with a restrictive manual approval handler.");

            config.AddCommand<AppServerSkillsAppsCommand>("appserver-skills-apps")
                .WithDescription("Start `codex app-server` and list skills and apps/connectors.");

            config.AddCommand<AppServerConfigReadCommand>("appserver-config")
                .WithDescription("Start `codex app-server` and read effective config + requirements + remote skills.");

            config.AddCommand<AppServerConfigWriteCommand>("appserver-config-write")
                .WithDescription("Start `codex app-server` and write remote skills / skills config (requires --apply and explicit --codex-home).");

            config.AddCommand<AppServerMcpManagementCommand>("appserver-mcp")
                .WithDescription("Start `codex app-server` and manage MCP servers (reload/status/oauth).");

            config.AddCommand<AppServerFuzzyFileSearchCommand>("appserver-fuzzy")
                .WithDescription("Start `codex app-server` and run an experimental fuzzy file search session.");

            config.AddCommand<AppServerTurnControlCommand>("appserver-turn-control")
                .WithDescription("Start `codex app-server` and demonstrate turn steering + interrupt.");

            config.AddCommand<AppServerReviewCommand>("appserver-review")
                .WithDescription("Start `codex app-server` and run a code review (review/start).");

            config.AddCommand<AppServerNotificationsCommand>("appserver-notifications")
                .WithDescription("Start `codex app-server` and observe global notifications (typed + raw).");

            config.AddBranch("appserver-thread", thread =>
            {
                thread.SetDescription("Explore Codex app-server thread endpoints.");

                thread.AddCommand<AppServerThreadStartCommand>("start")
                    .WithDescription("Start a new thread.");

                thread.AddCommand<AppServerThreadResumeCommand>("resume")
                    .WithDescription("Resume a thread by id (or rollout path when supported upstream).");

                thread.AddCommand<AppServerThreadListCommand>("list")
                    .WithDescription("List threads.");

                thread.AddCommand<AppServerThreadListLoadedCommand>("list-loaded")
                    .WithDescription("List loaded thread ids.");

                thread.AddCommand<AppServerThreadReadCommand>("read")
                    .WithDescription("Read a thread summary.");

                thread.AddCommand<AppServerThreadCompactCommand>("compact")
                    .WithDescription("Compact a thread.");

                thread.AddCommand<AppServerThreadRollbackCommand>("rollback")
                    .WithDescription("Rollback a thread by N turns.");

                thread.AddCommand<AppServerThreadForkCommand>("fork")
                    .WithDescription("Fork a thread (by id or rollout path when supported upstream).");

                thread.AddCommand<AppServerThreadArchiveCommand>("archive")
                    .WithDescription("Archive a thread.");

                thread.AddCommand<AppServerThreadUnarchiveCommand>("unarchive")
                    .WithDescription("Unarchive a thread.");

                thread.AddCommand<AppServerThreadSetNameCommand>("set-name")
                    .WithDescription("Set a thread name.");

                thread.AddCommand<AppServerThreadCleanBackgroundTerminalsCommand>("clean-bg-terminals")
                    .WithDescription("Clean background terminals for a thread.");
            });

            config.AddCommand<McpServerCommand>("mcpserver")
                .WithDescription("Start `codex mcp-server`, list tools, and run a small session.");

            config.AddCommand<StructuredReviewCommand>("structured-review")
                .WithDescription("Run a structured code review with typed output (issues + fix tasks).");

            config.AddCommand<DiOverridesCommand>("di-overrides")
                .WithDescription("Validate DI registration and override hooks (app-server + mcp-server).");
        });

        app.SetDefaultCommand<ExecCommand>();
        return await app.RunAsync(args);
    }
}
