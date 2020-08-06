﻿using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Progress;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace OmniSharp.Extensions.LanguageServer.Protocol.Server
{
    internal class GeneralLanguageServer : ServerProxyBase, IGeneralLanguageServer
    {
        public GeneralLanguageServer(IResponseRouter requestRouter, IProgressManager progressManager, IServerWorkDoneManager serverWorkDoneManager, ILanguageServerConfiguration languageServerConfiguration, ILanguageProtocolSettings settings) : base(requestRouter, progressManager, serverWorkDoneManager, languageServerConfiguration, settings)
        {
        }
    }
}
