﻿using DynamicData;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Progress;

namespace OmniSharp.Extensions.LanguageServer.Protocol.Client.WorkDone
{
    public interface IClientWorkDoneManager
    {
        void Initialize(WindowClientCapabilities windowClientCapabilities);
        bool IsSupported { get; }
        IObservableCache<IProgressObservable<WorkDoneProgress>, ProgressToken> PendingWork { get; }
        IProgressObservable<WorkDoneProgress> Monitor(ProgressToken progressToken);
    }
}
