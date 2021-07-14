Title: Source Generators
ShowInSidebar: true
Order: 4
---

# Source Generators
We have built a few source generators to help with aid with implementing the plumbing of all the things.  The goals of using source generation this way is to ensure that errors and mistakes are avoided and don't cause issues.

## Auto Implementation Properties
The following interfaces will automatically implement themselves so you don't have to worry about it.

* `IWorkDoneProgressParams`
* `IPartialItemsRequest`
* `IPartialItemRequest`
* `ITextDocumentRegistrationOptions`
* `IWorkDoneProgressOptions`
* `IStaticRegistrationOptions`

## JSON RPC Generation Attributes

The general JSON RPC Attributes have logic for LSP and DAP but that logic only kicks in in the correct types and/or attributes is in place.

### `[GenerateHandler([[["<namespace>"], Name = "<name>"], AllowDerivedRequests = true])]`
Generates an interface based on the given request object, within the optional namespace if provided.
You may also provide a specific name that will be used for the interface and base class names.  The name format is `I<name>Handler` and `<name>HandlerBase`.

There is special logic to handle request objects that use the `IPartialItemRequest<,>` or `IPartialItemsRequest<,>` interfaces.  This emit another base class `<name>PartialHandlerBase` that implements the right stuff for creating a handler that works with the partial spec.

Certain MediatR request types will map to different matters.
* `IRequest<TResponse>` - Will map as a request
* `IRequest` - Will map as a notification
* `IJsonRpcRequest` - Will map as a `Task` returning request.

Example Request Object:
```c#
    [Parallel]
    [Method(ClientNames.RegisterCapability, Direction.ServerToClient)]
    [
        GenerateHandler("OmniSharp.Extensions.LanguageServer.Protocol.Client", Name = "RegisterCapability"),
        GenerateHandlerMethods,
        GenerateRequestMethods(typeof(IClientLanguageServer), typeof(ILanguageServer))
    ]
    public class RegistrationParams : IJsonRpcRequest
    {
        public RegistrationContainer Registrations { get; set; } = null!;
    }
```

Example Output
```c#
#nullable enable
namespace OmniSharp.Extensions.LanguageServer.Protocol.Client
{
    [Parallel, Method(ClientNames.RegisterCapability, Direction.ServerToClient)]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
    public interface IRegisterCapabilityHandler : IJsonRpcRequestHandler<RegistrationParams, MediatR.Unit>
    {
    }

    [System.Runtime.CompilerServices.CompilerGeneratedAttribute, System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    abstract public class RegisterCapabilityHandlerBase : AbstractHandlers.Request<RegistrationParams, MediatR.Unit>, IRegisterCapabilityHandler
    {
    }
#nullable restore
```
Given `AllowDerivedRequests` an additional generic handler will be created.

Example Request Object:
```c#
    [Parallel]
    [Method(RequestNames.Launch, Direction.ClientToServer)]
    [
        GenerateHandler(Name = "Launch", AllowDerivedRequests = true),
        GenerateHandlerMethods,
        GenerateRequestMethods
    ]
    public class LaunchRequestArguments : IRequest<LaunchResponse>
    {
        /// <summary>
        /// If noDebug is true the launch request should launch the program without enabling debugging.
        /// </summary>
        [Optional]
        public bool NoDebug { get; set; }

        /// <summary>
        /// Optional data from the previous, restarted session.
        /// The data is sent as the 'restart' attribute of the 'terminated' event.
        /// The client should leave the data intact.
        /// </summary>
        [Optional]
        [JsonProperty(PropertyName = "__restart")]
        public JToken? Restart { get; set; }

        [JsonExtensionData] public IDictionary<string, object> ExtensionData { get; set; } = new Dictionary<string, object>();
    }

    public class LaunchResponse
    {
    }
```

Example Output:
```c#
    [Parallel, Method(RequestNames.Launch, Direction.ClientToServer)]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
    public interface ILaunchHandler<in T> : IJsonRpcRequestHandler<T, LaunchResponse> where T : LaunchRequestArguments
    {
    }

    [System.Runtime.CompilerServices.CompilerGeneratedAttribute, System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    abstract public class LaunchHandlerBase<T> : AbstractHandlers.Request<T, LaunchResponse>, ILaunchHandler<T> where T : LaunchRequestArguments
    {
    }

    public interface ILaunchHandler : ILaunchHandler<LaunchRequestArguments>
    {
    }

    [System.Runtime.CompilerServices.CompilerGeneratedAttribute, System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    abstract public class LaunchHandlerBase : LaunchHandlerBase<LaunchRequestArguments>, ILaunchHandler
    {
    }
```


### `[GenerateHandlerMethods([params Type[] registryTypes])]`
Generates helper methods for registering this as a delegate.  This is useful in more functional scenarios and more importantly in unit testing scenarios.

You can provide a list of registry types, these are the `this` item in a given extension method.  If not provided it will attempt to infer the usage.

Common registries are:
* `IJsonRpcServerRegistry`
* `ILanguageClientRegistry`
* `ILanguageServerRegistry`
* `IDebugAdapterClientRegistry`
* `IDebugAdapterServerRegistry`

Example request:

```c#
    [Parallel]
    [Method(ClientNames.RegisterCapability, Direction.ServerToClient)]
    [
        GenerateHandler("OmniSharp.Extensions.LanguageServer.Protocol.Client", Name = "RegisterCapability"),
        GenerateHandlerMethods
    ]
    public class RegistrationParams : IJsonRpcRequest
    {
        public RegistrationContainer Registrations { get; set; } = null!;
    }
```

Example output:

```c#
namespace OmniSharp.Extensions.LanguageServer.Protocol.Client
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute, System.Runtime.CompilerServices.CompilerGeneratedAttribute]
    public static partial class RegisterCapabilityExtensions
    {
        public static ILanguageClientRegistry OnRegisterCapability(this ILanguageClientRegistry registry, Func<RegistrationParams, Task> handler) => registry.AddHandler(ClientNames.RegisterCapability, new DelegatingHandlers.Request<RegistrationParams>(handler));
        public static ILanguageClientRegistry OnRegisterCapability(this ILanguageClientRegistry registry, Func<RegistrationParams, CancellationToken, Task> handler) => registry.AddHandler(ClientNames.RegisterCapability, new DelegatingHandlers.Request<RegistrationParams>(handler));
    }
}
```


### `[GenerateRequestMethods([params Type[] proxyTypes])]`
Generates helper methods for calling the notification or the delegate.  This is useful for hinting to the user what methods there are to call on a given class.

You can provide a list of proxy types, these are the `this` item in a given extension method.  If not provided it will attempt to infer the usage.

Common proxy types are anything that implements `IResponseRouter`:
  * `ILanguageProtocolProxy`
  * `ILanguageClientProxy`
    * `IClientLanguageClient`
    * `IGeneralLanguageClient`
    * `ITextDocumentLanguageClient`
    * `IWindowLanguageClient`
    * `IWorkspaceLanguageClient`
  * `ILanguageServerProxy`
    * `IClientLanguageServer`
    * `IGeneralLanguageServer`
    * `ITextDocumentLanguageServer`
    * `IWindowLanguageServer`
    * `IWorkspaceLanguageServer`
  * `IDebugAdapterProtocolProxy`
  * `IDebugAdapterClientProxy`
  * `IDebugAdapterServerProxy`

Example request:

```c#
    [Parallel]
    [Method(ClientNames.RegisterCapability, Direction.ServerToClient)]
    [
        GenerateHandler("OmniSharp.Extensions.LanguageServer.Protocol.Client", Name = "RegisterCapability"),
        GenerateRequestMethods(typeof(IClientLanguageServer), typeof(ILanguageServer))
    ]
    public class RegistrationParams : IJsonRpcRequest
    {
        public RegistrationContainer Registrations { get; set; } = null!;
    }
```

Example output:

```c#
namespace OmniSharp.Extensions.LanguageServer.Protocol.Client
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute, System.Runtime.CompilerServices.CompilerGeneratedAttribute]
    public static partial class RegisterCapabilityExtensions
    {
        public static Task RegisterCapability(this IClientLanguageServer mediator, RegistrationParams request, CancellationToken cancellationToken = default) => mediator.SendRequest(request, cancellationToken);
        public static Task RegisterCapability(this ILanguageServer mediator, RegistrationParams request, CancellationToken cancellationToken = default) => mediator.SendRequest(request, cancellationToken);
    }
}
```

## LSP Generation Attributes

### `CapabilityAttribute` / `RegistrationOptionsAttribute` / `ResolverAttribute`
These attributes are used to generate the "correct" pieces.  Some features do not have capabilities, some do not have registration, others both and some have none.

* `CapabilityAttribute` - Defines the capability type to be used.
* `RegistrationOptionsAttribute` - Defines the registration type to be used.
* `ResolverAttribute` - Defines the type that this feature will use it's resolved item
  * This is for the features that model around the resolve pattern, such as CodeLens or Completion.
  * This only works with that specific pattern

Example Request:
```c#
    namespace Models
    {
        [Parallel]
        [Method(TextDocumentNames.References, Direction.ClientToServer)]
        [
            GenerateHandler("OmniSharp.Extensions.LanguageServer.Protocol.Document", Name = "References"),
            GenerateHandlerMethods,
            GenerateRequestMethods(typeof(ITextDocumentLanguageClient), typeof(ILanguageClient))
        ]
        [RegistrationOptions(typeof(ReferenceRegistrationOptions)), Capability(typeof(ReferenceCapability))]
        public partial class ReferenceParams : TextDocumentPositionParams, IWorkDoneProgressParams, IPartialItemsRequest<LocationContainer, Location>
        {
            public ReferenceContext Context { get; set; } = null!;
        }
        public class ReferenceContext
        {
            /// <summary>
            /// Include the declaration of the current symbol.
            /// </summary>
            public bool IncludeDeclaration { get; set; }
        }

        [GenerateRegistrationOptions(nameof(ServerCapabilities.ReferencesProvider))]
        public partial class ReferenceRegistrationOptions : ITextDocumentRegistrationOptions, IWorkDoneProgressOptions { }
    }

    namespace Client.Capabilities
    {
        [CapabilityKey(nameof(ClientCapabilities.TextDocument), nameof(TextDocumentClientCapabilities.References))]
        public partial class ReferenceCapability : DynamicCapability, ConnectedCapability<IReferencesHandler>
        {
        }
    }
```

Example Output:
```c#
namespace OmniSharp.Extensions.LanguageServer.Protocol.Models
{
    public partial class ReferenceParams
    {
        [Optional]
        public ProgressToken? WorkDoneToken
        {
            get;
            set;
        }

        [Optional]
        public ProgressToken? PartialResultToken
        {
            get;
            set;
        }
    }
}

namespace OmniSharp.Extensions.LanguageServer.Protocol.Document
{
    [Parallel, Method(TextDocumentNames.References, Direction.ClientToServer)]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
    public partial interface IReferencesHandler : IJsonRpcRequestHandler<ReferenceParams, LocationContainer>, IRegistration<ReferenceRegistrationOptions, ReferenceCapability>
    {
    }

    [System.Runtime.CompilerServices.CompilerGeneratedAttribute, System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    abstract public partial class ReferencesHandlerBase : AbstractHandlers.Request<ReferenceParams, LocationContainer, ReferenceRegistrationOptions, ReferenceCapability>, IReferencesHandler
    {
    }

    [System.Runtime.CompilerServices.CompilerGeneratedAttribute, System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    abstract public partial class ReferencesPartialHandlerBase : AbstractHandlers.PartialResults<ReferenceParams, LocationContainer, Location, ReferenceRegistrationOptions, ReferenceCapability>, IReferencesHandler
    {
        protected ReferencesPartialHandlerBase(System.Guid id, IProgressManager progressManager): base(progressManager, LocationContainer.From)
        {
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute, System.Runtime.CompilerServices.CompilerGeneratedAttribute]
    public static partial class ReferencesExtensions
    {
        public static ILanguageServerRegistry OnReferences(this ILanguageServerRegistry registry, Func<ReferenceParams, Task<LocationContainer>> handler, Func<ReferenceCapability, ReferenceRegistrationOptions> registrationOptions)
        {
            return registry.AddHandler(TextDocumentNames.References, new LanguageProtocolDelegatingHandlers.Request<ReferenceParams, LocationContainer, ReferenceRegistrationOptions, ReferenceCapability>(HandlerAdapter<ReferenceCapability>.Adapt<ReferenceParams, LocationContainer>(handler), RegistrationAdapter<ReferenceCapability>.Adapt<ReferenceRegistrationOptions>(registrationOptions)));
        }

        public static ILanguageServerRegistry OnReferences(this ILanguageServerRegistry registry, Func<ReferenceParams, CancellationToken, Task<LocationContainer>> handler, Func<ReferenceCapability, ReferenceRegistrationOptions> registrationOptions)
        {
            return registry.AddHandler(TextDocumentNames.References, new LanguageProtocolDelegatingHandlers.Request<ReferenceParams, LocationContainer, ReferenceRegistrationOptions, ReferenceCapability>(HandlerAdapter<ReferenceCapability>.Adapt<ReferenceParams, LocationContainer>(handler), RegistrationAdapter<ReferenceCapability>.Adapt<ReferenceRegistrationOptions>(registrationOptions)));
        }

        public static ILanguageServerRegistry OnReferences(this ILanguageServerRegistry registry, Func<ReferenceParams, ReferenceCapability, CancellationToken, Task<LocationContainer>> handler, Func<ReferenceCapability, ReferenceRegistrationOptions> registrationOptions)
        {
            return registry.AddHandler(TextDocumentNames.References, new LanguageProtocolDelegatingHandlers.Request<ReferenceParams, LocationContainer, ReferenceRegistrationOptions, ReferenceCapability>(HandlerAdapter<ReferenceCapability>.Adapt<ReferenceParams, LocationContainer>(handler), RegistrationAdapter<ReferenceCapability>.Adapt<ReferenceRegistrationOptions>(registrationOptions)));
        }

        public static ILanguageServerRegistry ObserveReferences(this ILanguageServerRegistry registry, Action<ReferenceParams, IObserver<IEnumerable<Location>>> handler, Func<ReferenceCapability, ReferenceRegistrationOptions> registrationOptions)
        {
            return registry.AddHandler(TextDocumentNames.References, _ => new LanguageProtocolDelegatingHandlers.PartialResults<ReferenceParams, LocationContainer, Location, ReferenceRegistrationOptions, ReferenceCapability>(PartialAdapter<ReferenceCapability>.Adapt<ReferenceParams, Location>(handler), RegistrationAdapter<ReferenceCapability>.Adapt<ReferenceRegistrationOptions>(registrationOptions), _.GetService<IProgressManager>(), LocationContainer.From));
        }

        public static ILanguageServerRegistry ObserveReferences(this ILanguageServerRegistry registry, Action<ReferenceParams, IObserver<IEnumerable<Location>>, CancellationToken> handler, Func<ReferenceCapability, ReferenceRegistrationOptions> registrationOptions)
        {
            return registry.AddHandler(TextDocumentNames.References, _ => new LanguageProtocolDelegatingHandlers.PartialResults<ReferenceParams, LocationContainer, Location, ReferenceRegistrationOptions, ReferenceCapability>(PartialAdapter<ReferenceCapability>.Adapt<ReferenceParams, Location>(handler), RegistrationAdapter<ReferenceCapability>.Adapt<ReferenceRegistrationOptions>(registrationOptions), _.GetService<IProgressManager>(), LocationContainer.From));
        }

        public static ILanguageServerRegistry ObserveReferences(this ILanguageServerRegistry registry, Action<ReferenceParams, IObserver<IEnumerable<Location>>, ReferenceCapability, CancellationToken> handler, Func<ReferenceCapability, ReferenceRegistrationOptions> registrationOptions)
        {
            return registry.AddHandler(TextDocumentNames.References, _ => new LanguageProtocolDelegatingHandlers.PartialResults<ReferenceParams, LocationContainer, Location, ReferenceRegistrationOptions, ReferenceCapability>(PartialAdapter<ReferenceCapability>.Adapt<ReferenceParams, Location>(handler), RegistrationAdapter<ReferenceCapability>.Adapt<ReferenceRegistrationOptions>(registrationOptions), _.GetService<IProgressManager>(), LocationContainer.From));
        }

        public static IRequestProgressObservable<IEnumerable<Location>, LocationContainer> RequestReferences(this ITextDocumentLanguageClient mediator, ReferenceParams request, CancellationToken cancellationToken = default) => mediator.ProgressManager.MonitorUntil(request, value => new LocationContainer(value), cancellationToken);
        public static IRequestProgressObservable<IEnumerable<Location>, LocationContainer> RequestReferences(this ILanguageClient mediator, ReferenceParams request, CancellationToken cancellationToken = default) => mediator.ProgressManager.MonitorUntil(request, value => new LocationContainer(value), cancellationToken);
    }
}
```

### `GenerateTypedDataAttribute`

This leverages two interfaces `ICanHaveData` and `ICanBeResolved` they are identical interfaces but just a slightly different in overall meaning.

This attributes takes a class that implements that interface and creates a copy of the class with one generic type parameter.
Then implements all the required logic to make that type work and interact with it's non-strongly typed friend.  This includes methods and implict operators for conversion.

Example Object:
```c#
    /// <summary>
    /// A code lens represents a command that should be shown along with
    /// source text, like the number of references, a way to run tests, etc.
    ///
    /// A code lens is _unresolved_ when no command is associated to it. For performance
    /// reasons the creation of a code lens and resolving should be done in two stages.
    /// </summary>
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    [Parallel]
    [Method(TextDocumentNames.CodeLensResolve, Direction.ClientToServer)]
    [
        GenerateHandler("OmniSharp.Extensions.LanguageServer.Protocol.Document", Name = "CodeLensResolve"),
        GenerateHandlerMethods,
        GenerateRequestMethods(typeof(ITextDocumentLanguageClient), typeof(ILanguageClient)),
        GenerateTypedData,
        GenerateContainer
    ]
    [RegistrationOptions(typeof(CodeLensRegistrationOptions)), Capability(typeof(CodeLensCapability))]
    public partial class CodeLens : IRequest<CodeLens>, ICanBeResolved
    {
        /// <summary>
        /// The range in which this code lens is valid. Should only span a single line.
        /// </summary>
        public Range Range { get; set; } = null!;

        /// <summary>
        /// The command this code lens represents.
        /// </summary>
        [Optional]
        public Command? Command { get; set; }

        /// <summary>
        /// A data entry field that is preserved on a code lens item between
        /// a code lens and a code lens resolve request.
        /// </summary>
        [Optional]
        public JToken? Data { get; set; }

        private string DebuggerDisplay => $"{Range}{( Command != null ? $" {Command}" : "" )}";

        /// <inheritdoc />
        public override string ToString() => DebuggerDisplay;
    }
```

Example Response:
```c#
namespace OmniSharp.Extensions.LanguageServer.Protocol.Models
{
    public partial class CodeLens
    {
        public CodeLens<TData> WithData<TData>(TData data)
            where TData : HandlerIdentity?
        {
            return new CodeLens<TData>{Range = Range, Command = Command, Data = data};
        }

        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("item")]
        public static CodeLens? From<T>(CodeLens<T>? item)
            where T : HandlerIdentity? => item switch
        {
        not null => item, _ => null
        }

        ;
    }

    /// <summary>
    /// A code lens represents a command that should be shown along with
    /// source text, like the number of references, a way to run tests, etc.
    ///
    /// A code lens is _unresolved_ when no command is associated to it. For performance
    /// reasons the creation of a code lens and resolving should be done in two stages.
    /// </summary>
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    [Parallel]
    [RegistrationOptions(typeof(CodeLensRegistrationOptions)), Capability(typeof(CodeLensCapability))]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute, System.Runtime.CompilerServices.CompilerGeneratedAttribute]
    public partial class CodeLens<T> : ICanBeResolved where T : HandlerIdentity?
    {
        /// <summary>
        /// The range in which this code lens is valid. Should only span a single line.
        /// </summary>
        public Range Range
        {
            get;
            set;
        }

        = null !;
        /// <summary>
        /// The command this code lens represents.
        /// </summary>
        [Optional]
        public Command? Command
        {
            get;
            set;
        }

        /// <summary>
        /// A data entry field that is preserved on a code lens item between
        /// a code lens and a code lens resolve request.
        /// </summary>
        [Optional]
        public T Data
        {
            get => ((ICanBeResolved)this).Data?.ToObject<T>()!;
            set => ((ICanBeResolved)this).Data = JToken.FromObject(value);
        }

        private string DebuggerDisplay => $"{Range}{(Command != null ? $" {Command}" : "")}";
        /// <inheritdoc />
        public override string ToString() => DebuggerDisplay;
        public CodeLens<TData> WithData<TData>(TData data)
            where TData : HandlerIdentity?
        {
            return new CodeLens<TData>{Range = Range, Command = Command, Data = data};
        }

        JToken? ICanBeResolved.Data
        {
            get;
            set;
        }

        private JToken? JData
        {
            get => ((ICanBeResolved)this).Data;
            set => ((ICanBeResolved)this).Data = value;
        }

        public static implicit operator CodeLens<T>(CodeLens value) => new CodeLens<T>{Range = value.Range, Command = value.Command, JData = ((ICanBeResolved)value).Data};
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("value")]
        public static implicit operator CodeLens? (CodeLens<T>? value) => value switch
        {
        not null => new CodeLens{Range = value.Range, Command = value.Command, Data = ((ICanBeResolved)value).Data}, _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("item")]
        public static CodeLens<T>? From(CodeLens? item) => item switch
        {
        not null => item, _ => null
        }

        ;
    }

    public partial class CodeLensContainer<T> : ContainerBase<CodeLens<T>> where T : HandlerIdentity?
    {
        public CodeLensContainer(): this(Enumerable.Empty<CodeLens<T>>())
        {
        }

        public CodeLensContainer(IEnumerable<CodeLens<T>> items): base(items)
        {
        }

        public CodeLensContainer(params CodeLens<T>[] items): base(items)
        {
        }

        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static CodeLensContainer<T>? From(IEnumerable<CodeLens<T>>? items) => items switch
        {
        not null => new CodeLensContainer<T>(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static implicit operator CodeLensContainer<T>? (CodeLens<T>[] items) => items switch
        {
        not null => new CodeLensContainer<T>(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static CodeLensContainer<T>? From(params CodeLens<T>[] items) => items switch
        {
        not null => new CodeLensContainer<T>(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static implicit operator CodeLensContainer<T>? (Collection<CodeLens<T>>? items) => items switch
        {
        not null => new CodeLensContainer<T>(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static CodeLensContainer<T>? From(Collection<CodeLens<T>>? items) => items switch
        {
        not null => new CodeLensContainer<T>(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static implicit operator CodeLensContainer<T>? (List<CodeLens<T>>? items) => items switch
        {
        not null => new CodeLensContainer<T>(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static CodeLensContainer<T>? From(List<CodeLens<T>>? items) => items switch
        {
        not null => new CodeLensContainer<T>(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static implicit operator CodeLensContainer<T>? (in ImmutableArray<CodeLens<T>>? items) => items switch
        {
        not null => new CodeLensContainer<T>(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static CodeLensContainer<T>? From(in ImmutableArray<CodeLens<T>>? items) => items switch
        {
        not null => new CodeLensContainer<T>(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static implicit operator CodeLensContainer<T>? (ImmutableList<CodeLens<T>>? items) => items switch
        {
        not null => new CodeLensContainer<T>(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static CodeLensContainer<T>? From(ImmutableList<CodeLens<T>>? items) => items switch
        {
        not null => new CodeLensContainer<T>(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("container")]
        public static implicit operator CodeLensContainer? (CodeLensContainer<T>? container) => container switch
        {
        not null => new CodeLensContainer(container.Select(value => (CodeLens)value)), _ => null
        }

        ;
    }
}
```

### `GenerateContainerAttribute`

This is very simply it creates a class that derives from `ContainerBase<T>` and implements conversion methods and operators.

This leverages two interfaces `ICanHaveData` and `ICanBeResolved` they are identical interfaces but just a slightly different in overall meaning.

This attributes takes a class that implements that interface and creates a copy of the class with one generic type parameter.
Then implements all the required logic to make that type work and interact with it's non-strongly typed friend.  This includes methods and implict operators for conversion.

Example Object:
```c#
    /// <summary>
    /// A code lens represents a command that should be shown along with
    /// source text, like the number of references, a way to run tests, etc.
    ///
    /// A code lens is _unresolved_ when no command is associated to it. For performance
    /// reasons the creation of a code lens and resolving should be done in two stages.
    /// </summary>
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    [Parallel]
    [Method(TextDocumentNames.CodeLensResolve, Direction.ClientToServer)]
    [
        GenerateHandler("OmniSharp.Extensions.LanguageServer.Protocol.Document", Name = "CodeLensResolve"),
        GenerateHandlerMethods,
        GenerateRequestMethods(typeof(ITextDocumentLanguageClient), typeof(ILanguageClient)),
        GenerateTypedData,
        GenerateContainer
    ]
    [RegistrationOptions(typeof(CodeLensRegistrationOptions)), Capability(typeof(CodeLensCapability))]
    public partial class CodeLens : IRequest<CodeLens>, ICanBeResolved
    {
        /// <summary>
        /// The range in which this code lens is valid. Should only span a single line.
        /// </summary>
        public Range Range { get; set; } = null!;

        /// <summary>
        /// The command this code lens represents.
        /// </summary>
        [Optional]
        public Command? Command { get; set; }

        /// <summary>
        /// A data entry field that is preserved on a code lens item between
        /// a code lens and a code lens resolve request.
        /// </summary>
        [Optional]
        public JToken? Data { get; set; }

        private string DebuggerDisplay => $"{Range}{( Command != null ? $" {Command}" : "" )}";

        /// <inheritdoc />
        public override string ToString() => DebuggerDisplay;
    }
```

Example Response:
```c#
namespace OmniSharp.Extensions.LanguageServer.Protocol.Models
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute, System.Runtime.CompilerServices.CompilerGeneratedAttribute]
    public partial class CodeActionContainer : ContainerBase<CodeAction>
    {
        public CodeActionContainer(): this(Enumerable.Empty<CodeAction>())
        {
        }

        public CodeActionContainer(IEnumerable<CodeAction> items): base(items)
        {
        }

        public CodeActionContainer(params CodeAction[] items): base(items)
        {
        }

        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static CodeActionContainer? From(IEnumerable<CodeAction>? items) => items switch
        {
        not null => new CodeActionContainer(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static implicit operator CodeActionContainer? (CodeAction[] items) => items switch
        {
        not null => new CodeActionContainer(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static CodeActionContainer? From(params CodeAction[] items) => items switch
        {
        not null => new CodeActionContainer(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static implicit operator CodeActionContainer? (Collection<CodeAction>? items) => items switch
        {
        not null => new CodeActionContainer(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static CodeActionContainer? From(Collection<CodeAction>? items) => items switch
        {
        not null => new CodeActionContainer(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static implicit operator CodeActionContainer? (List<CodeAction>? items) => items switch
        {
        not null => new CodeActionContainer(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static CodeActionContainer? From(List<CodeAction>? items) => items switch
        {
        not null => new CodeActionContainer(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static implicit operator CodeActionContainer? (in ImmutableArray<CodeAction>? items) => items switch
        {
        not null => new CodeActionContainer(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static CodeActionContainer? From(in ImmutableArray<CodeAction>? items) => items switch
        {
        not null => new CodeActionContainer(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static implicit operator CodeActionContainer? (ImmutableList<CodeAction>? items) => items switch
        {
        not null => new CodeActionContainer(items), _ => null
        }

        ;
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("items")]
        public static CodeActionContainer? From(ImmutableList<CodeAction>? items) => items switch
        {
        not null => new CodeActionContainer(items), _ => null
        }

        ;
    }
}
```

### `GenerateRegistrationOptionsAttribute`
This attribute is used to the static version of the registration options and generate a default converter if none is provided.

Example Options
```c#
    [GenerateRegistrationOptions(nameof(ServerCapabilities.ImplementationProvider))]
    public partial class ImplementationRegistrationOptions : ITextDocumentRegistrationOptions, IWorkDoneProgressOptions, IStaticRegistrationOptions { }
```

Example Output:
```c#
    [RegistrationOptionsConverterAttribute(typeof(ImplementationRegistrationOptionsConverter))]
    public partial class ImplementationRegistrationOptions : OmniSharp.Extensions.LanguageServer.Protocol.IRegistrationOptions
    {
        public DocumentSelector? DocumentSelector
        {
            get;
            set;
        }

        [Optional]
        public bool WorkDoneProgress
        {
            get;
            set;
        }

        [Optional]
        public string? Id
        {
            get;
            set;
        }

        class ImplementationRegistrationOptionsConverter : RegistrationOptionsConverterBase<ImplementationRegistrationOptions, StaticOptions>
        {
            public ImplementationRegistrationOptionsConverter(): base(nameof(ServerCapabilities.ImplementationProvider))
            {
            }

            public override StaticOptions Convert(ImplementationRegistrationOptions source)
            {
                return new StaticOptions{WorkDoneProgress = source.WorkDoneProgress, Id = source.Id};
            }
        }

        public partial class StaticOptions : IWorkDoneProgressOptions, IStaticRegistrationOptions
        {
            [Optional]
            public bool WorkDoneProgress
            {
                get;
                set;
            }

            [Optional]
            public string? Id
            {
                get;
                set;
            }
        }
    }
```
