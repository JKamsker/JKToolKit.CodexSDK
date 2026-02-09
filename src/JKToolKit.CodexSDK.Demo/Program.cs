using JKToolKit.CodexSDK.Demo.Commands.AppServerApproval;
using JKToolKit.CodexSDK.Demo.Commands.AppServerStream;
using JKToolKit.CodexSDK.Demo.Commands.Exec;
using JKToolKit.CodexSDK.Demo.Commands.McpServer;
using JKToolKit.CodexSDK.Demo.Commands.Review;
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

            config.AddCommand<ReviewCommand>("review")
                .WithDescription("Run a non-interactive `codex review` and print stdout/stderr.");

            config.AddCommand<AppServerStreamCommand>("appserver-stream")
                .WithDescription("Start `codex app-server` and stream turn output.");

            config.AddCommand<AppServerApprovalCommand>("appserver-approval")
                .WithDescription("Start `codex app-server` with a restrictive manual approval handler.");

            config.AddCommand<McpServerCommand>("mcpserver")
                .WithDescription("Start `codex mcp-server`, list tools, and run a small session.");
        });

        app.SetDefaultCommand<ExecCommand>();
        return await app.RunAsync(args);
    }
}
