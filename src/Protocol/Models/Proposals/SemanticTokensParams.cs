﻿using System;
using MediatR;

namespace OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals
{
    /// <summary>
    /// @since 3.16.0
    /// </summary>
    [Obsolete(Constants.Proposal)]
    public class SemanticTokensParams : IWorkDoneProgressParams, ITextDocumentIdentifierParams,
        IPartialItem<SemanticTokensPartialResult>, IRequest<SemanticTokens>
    {
        /// <summary>
        /// The text document.
        /// </summary>
        public TextDocumentIdentifier TextDocument { get; set; }

        public ProgressToken WorkDoneToken { get; set; }
        public ProgressToken PartialResultToken { get; set; }
    }
}