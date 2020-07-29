using System;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.DebugAdapter.Client;
using OmniSharp.Extensions.JsonRpc;

namespace OmniSharp.Extensions.DebugAdapter.Protocol
{
    public interface IDebugAdapterClient : IResponseRouter, IDisposable
    {
        Task Initialize(CancellationToken token);
        IClientProgressManager ProgressManager { get; }
    }
}
