using System.Collections.Generic;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities
{
    public class DocumentRangeFormattingOptions : WorkDoneProgressOptions, IDocumentRangeFormattingOptions
    {
        public static DocumentRangeFormattingOptions Of(IDocumentRangeFormattingOptions options, IEnumerable<IHandlerDescriptor> descriptors)
        {
            return new DocumentRangeFormattingOptions()
            {
                WorkDoneProgress = options.WorkDoneProgress,

            };
        }
    }
}
