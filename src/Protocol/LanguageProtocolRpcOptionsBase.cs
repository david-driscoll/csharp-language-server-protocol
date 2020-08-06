﻿using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;

namespace OmniSharp.Extensions.LanguageServer.Protocol
{
    public abstract class LanguageProtocolRpcOptionsBase<T> : JsonRpcServerOptionsBase<T> where T : IJsonRpcHandlerRegistry<T>
    {
        public LanguageProtocolRpcOptionsBase()
        {
            Services.AddLogging(builder => LoggingBuilderAction?.Invoke(builder));
        }

        public T AddTextDocumentIdentifier(params ITextDocumentIdentifier[] handlers)
        {
            foreach (var item in handlers)
            {
                Services.AddSingleton(typeof(ITextDocumentIdentifier), item);
            }

            return (T) (object) this;
        }

        public T AddTextDocumentIdentifier<TTextDocumentIdentifier>() where TTextDocumentIdentifier : ITextDocumentIdentifier
        {
            Services.AddSingleton(typeof(ITextDocumentIdentifier), typeof(TTextDocumentIdentifier));
            return (T) (object) this;
        }

        public Protocol.Serialization.ISerializer Serializer { get; set; } = new Protocol.Serialization.Serializer(ClientVersion.Lsp3);
        internal bool AddDefaultLoggingProvider { get; set; }
        internal Action<ILoggingBuilder> LoggingBuilderAction { get; set; } = _ => { };
        internal Action<IConfigurationBuilder> ConfigurationBuilderAction { get; set; } = _ => { };
    }
}
