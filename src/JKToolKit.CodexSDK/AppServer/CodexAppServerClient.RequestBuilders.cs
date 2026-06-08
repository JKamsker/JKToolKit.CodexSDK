using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    internal static TurnSteerParams BuildTurnSteerParams(TurnSteerOptions options) =>
        new()
        {
            ThreadId = options.ThreadId,
            ExpectedTurnId = options.ExpectedTurnId,
            ClientUserMessageId = options.ClientUserMessageId,
            Input = options.Input.Select(i => i.Wire).ToArray(),
            ResponsesApiClientMetadata = options.ResponsesApiClientMetadata,
            AdditionalContext = CodexAppServerWireBuilders.BuildAdditionalContext(
                options.AdditionalContext,
                nameof(TurnSteerOptions.AdditionalContext))
        };

    internal static ReviewStartParams BuildReviewStartParams(ReviewStartOptions options) =>
        new()
        {
            ThreadId = options.ThreadId,
            Target = options.Target.ToWire(),
            Delivery = options.Delivery switch
            {
                ReviewDelivery.Inline => "inline",
                ReviewDelivery.Detached => "detached",
                _ => null
            }
        };
}
