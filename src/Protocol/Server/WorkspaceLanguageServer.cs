using System;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Progress;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace OmniSharp.Extensions.LanguageServer.Protocol.Server
{
    internal class WorkspaceLanguageServer : ServerProxyBase, IWorkspaceLanguageServer
    {
        public WorkspaceLanguageServer(IResponseRouter requestRouter, IProgressManager progressManager, Lazy<IServerWorkDoneManager> serverWorkDoneManager, Lazy<ILanguageServerConfiguration> languageServerConfiguration, ILanguageProtocolSettings settings) : base(requestRouter, progressManager, serverWorkDoneManager, languageServerConfiguration, settings)
        {
        }
    }
}
