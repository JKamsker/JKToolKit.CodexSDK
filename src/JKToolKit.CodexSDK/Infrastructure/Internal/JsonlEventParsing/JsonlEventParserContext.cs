using Microsoft.Extensions.Logging;
using JKToolKit.CodexSDK.Exec.Overrides;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

internal readonly record struct JsonlEventParserContext(
    ILogger Logger,
    IReadOnlyList<IExecEventTransformer>? Transformers,
    IReadOnlyList<IExecEventMapper>? Mappers);

