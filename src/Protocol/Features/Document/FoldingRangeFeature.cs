using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Generation;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

// ReSharper disable once CheckNamespace
namespace OmniSharp.Extensions.LanguageServer.Protocol
{
    namespace Models
    {
        [Parallel]
        [Method(TextDocumentNames.FoldingRange, Direction.ClientToServer)]
        [
            GenerateHandler("OmniSharp.Extensions.LanguageServer.Protocol.Document", Name = "FoldingRange"),
            GenerateHandlerMethods,
            GenerateRequestMethods(typeof(ITextDocumentLanguageClient), typeof(ILanguageClient))
        ]
        [RegistrationOptions(typeof(FoldingRangeRegistrationOptions)), Capability(typeof(FoldingRangeCapability))]
        public partial class FoldingRangeRequestParam : ITextDocumentIdentifierParams, IPartialItemsRequest<Container<FoldingRange>?, FoldingRange>, IWorkDoneProgressParams
        {
            /// <summary>
            /// The text document.
            /// </summary>
            public TextDocumentIdentifier TextDocument { get; set; } = null!;
        }

        /// <summary>
        /// Represents a folding range.
        /// </summary>
        [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
        public partial class FoldingRange
        {
            /// <summary>
            /// The zero-based line number from where the folded range starts.
            /// </summary>
            /// <remarks>
            /// TODO: UPDATE THIS next version
            /// <see cref="uint"/> in the LSP spec
            /// </remarks>
            public long StartLine { get; set; }

            /// <summary>
            /// The zero-based character offset from where the folded range starts. If not defined, defaults to the length of the start line.
            /// </summary>
            /// <remarks>
            /// TODO: UPDATE THIS next version
            /// <see cref="uint"/> in the LSP spec
            /// </remarks>
            [Optional]
            public long? StartCharacter { get; set; }

            /// <summary>
            /// The zero-based line number where the folded range ends.
            /// </summary>
            /// <remarks>
            /// TODO: UPDATE THIS next version
            /// <see cref="uint"/> in the LSP spec
            /// </remarks>
            public long EndLine { get; set; }

            /// <summary>
            /// The zero-based character offset before the folded range ends. If not defined, defaults to the length of the end line.
            /// </summary>
            /// <remarks>
            /// TODO: UPDATE THIS next version
            /// <see cref="uint"/> in the LSP spec
            /// </remarks>
            [Optional]
            public long? EndCharacter { get; set; }

            /// <summary>
            /// Describes the kind of the folding range such as `comment' or 'region'. The kind
            /// is used to categorize folding ranges and used by commands like 'Fold all comments'. See
            /// [FoldingRangeKind](#FoldingRangeKind) for an enumeration of standardized kinds.
            /// </summary>
            [Optional]
            public FoldingRangeKind? Kind { get; set; }

            private string DebuggerDisplay =>
                $"[start: (line: {StartLine}{( StartCharacter.HasValue ? $", char: {StartCharacter}" : string.Empty )}), end: (line: {EndLine}, char: {( EndCharacter.HasValue ? $", char: {EndCharacter}" : string.Empty )})]";

            /// <inheritdoc />
            public override string ToString() => DebuggerDisplay;
        }

        [GenerateRegistrationOptions(nameof(ServerCapabilities.FoldingRangeProvider))]
        public partial class FoldingRangeRegistrationOptions : ITextDocumentRegistrationOptions, IWorkDoneProgressOptions, IStaticRegistrationOptions
        {
        }
    }

    namespace Client.Capabilities
    {
        [CapabilityKey(nameof(ClientCapabilities.TextDocument), nameof(TextDocumentClientCapabilities.FoldingRange))]
        public partial class FoldingRangeCapability : DynamicCapability, ConnectedCapability<IFoldingRangeHandler>
        {
            /// <summary>
            /// The maximum number of folding ranges that the client prefers to receive per document. The value serves as a
            /// hint, servers are free to follow the limit.
            /// </summary>
            /// <remarks>
            /// <see cref="uint"/> in the LSP spec
            /// </remarks>
            [Optional]
            public int? RangeLimit { get; set; }

            /// <summary>
            /// If set, the client signals that it only supports folding complete lines. If set, client will
            /// ignore specified `startCharacter` and `endCharacter` properties in a FoldingRange.
            /// </summary>
            public bool LineFoldingOnly { get; set; }
        }
    }

    namespace Document
    {
    }
}
