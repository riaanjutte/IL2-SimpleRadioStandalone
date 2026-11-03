using System.Collections.Generic;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Api
{
    public sealed record ErrorResponse(string Error);

    /// <summary>A rejected request: the first problem, and every problem keyed by field.</summary>
    public sealed record ValidationErrorResponse(string Error, IReadOnlyDictionary<string, string> Errors);
}
