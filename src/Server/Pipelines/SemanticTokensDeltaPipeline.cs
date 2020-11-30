﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace OmniSharp.Extensions.LanguageServer.Server.Pipelines
{
    [Obsolete(Constants.Proposal)]
    class SemanticTokensDeltaPipeline<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse?>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
        {
            if (request is SemanticTokensParams semanticTokensParams)
            {
                var response = await next().ConfigureAwait(false);
                if (GetResponse(semanticTokensParams, response, out var result) && string.IsNullOrEmpty(result.ResultId))
                {
                    result = result with { ResultId = Guid.NewGuid().ToString() };
                }

                return result is TResponse r ? r : response;
            }

            if (request is SemanticTokensDeltaParams semanticTokensDeltaParams)
            {
                var response = await next().ConfigureAwait(false);
                if (GetResponse(semanticTokensDeltaParams, response, out var result))
                {
                    if (result.IsFull && string.IsNullOrEmpty(result.Full!.ResultId))
                    {
                        result = result with { Full = result.Full with { ResultId = semanticTokensDeltaParams.PreviousResultId } };
                    }
                    else if (result.IsDelta && string.IsNullOrEmpty(result.Delta!.ResultId))
                    {
                        result = result with { Delta = result.Delta with {ResultId = semanticTokensDeltaParams.PreviousResultId} };
                    }
                }

                return result is TResponse r ? r : response;
            }

            return await next().ConfigureAwait(false);
        }

        private bool GetResponse<TR>(IRequest<TR> request, object? response, [NotNullWhen(true)] out TR result)
        {
            if (response is TR r)
            {
                result = r;
                return true;
            }

            result = default!;
            return false;
        }

        private TR ToResponse<TR>(IRequest<TR> request, object? response)
        {
            if (response is TR r)
            {
                return r;
            }

            return default!;
        }
    }
}
