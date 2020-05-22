﻿using System;
using OmniSharp.Extensions.JsonRpc;

namespace OmniSharp.Extensions.LanguageServer.Protocol.Shared
{
    public interface ISupportedCapabilities
    {
        bool AllowsDynamicRegistration(Type capabilityType);
        void SetCapability(ILspHandlerDescriptor descriptor, IJsonRpcHandler handler);
    }
}
