Title: Language Server Protocol
ShowInSidebar: true
Order: 1
---

# Implementing the Language Server Protocol

The goal of this library is to implement [Language Server Protocol](https://microsoft.github.io/language-server-protocol/) as closely as possible

Included in this library is a full-fidelity `LanguageServer` but also full `LanguageClient` implementation that could be implemented in an editor, but mainly it is used to help make Unit Testing easier, more consistent (and maybe even fun!).

# Concepts
The language server is built oin a few concepts.  At it's core is the [MediatR](https://github.com/jbogard/MediatR) library that you will build language specific handlers around. Around that core is a bunch of knowledge of the LSP protocol
with the goal of making it more ".NET" like and less protocol centric.

LSP revolves around features ( eg Completion, Hover, etc ) that define the inputs (request object) the outputs (response object) as well as Client Capabilities and Server Registration Options.

## Client Capabilities
These determine what features are supported by the client, and each has a different set of capabilities. The [specification](https://microsoft.github.io/language-server-protocol/) explains each feature and the requirements of each.

## Server Registration Options
The protocol defines two kinds of registration, static and dynamic.  Dynamic registration, when it's supported, allows you to register features with the client on demand.  Static registration is returned to the client during the initialization phase that explains what features the server supports.  Dynamic and Static registration cannot be mixed.  If you register something statically, you cannot register the same thing dynamically, otherwise the client will register both features twice.

# Creating a Language Server or Client
`LanguageServer` or `LanguageClient` can be created through two methods.

## Standalone

> `LanguageServer.Create(options => {})` / `LanguageClient.Create(options => {})`
This will create a server where you provide options, handlers and more. An optional `IServiceProvider` can be provided that will be used as a fallback container when `IJsonRcpHandlers` are being resolved.

## Microsoft.Extensions.DependencyInjection

> `services.AddLanguageServer([string name, ], options => {})` / `services.AddLanguageClient([string name, ], options => {})`
This will be added to your service collection, with multiples supported.

* In the event that you add multiple named servers, they must be resolved using `LanguageServerResolver` / `LanguageClientResolver`.

When created through Microsoft DI the server will use the `IServiceProvider` as a fallback when resolving `IJsonRpcHandlers`.

## Options

Some of the important options include...

* `options.WithInput()` takes an input `Stream` or `PipeReader`
* `options.WithOutput()` takes an output `Stream` or `PipeWriter`
* `options.WithAssemblies()` takes additional assemblies that will participate in scanning operations.
  * Sometimes we scan this list of assemblies for potential strongly typed requests and notifications
* `options.WithServerInfo()` / `options.WithClientInfo()`
  * This surfaces the proper info object to both sides
* `options.AddTextDocumentIdentifier`
  * Text document identifiers are used to help in routing requests to a specific `DocumentSelector`.
* `options.AddHandler` allows you to add handlers that implement `IJsonRpcNotificationHandler<>`, `IJsonRpcRequestHandler<>`, `IJsonRpcRequestHandler<,>`.
  * Handlers can be added as an instance, type or a factory that is given a `IServiceProvider`
* `options.OnNotification` / `options.OnRequest`
  * These methods can be used to create handler delegates without having to implement the request interfaces.
* `options.OnJsonNotification` / `options.OnJsonRequest`
  * These methods can be used to create handler delegates without having to implement the request interfaces.
  * These json `JToken` an the request / response types.
* `options.WithMaximumRequestTimeout()`
  * Sets the maximum timeout before a request is cancelled
  * Defaults to 5 minutes
* `options.WithSupportsContentModified()`
  * Sets the into a special model that is used more for Language Server Protocol.
  * In this mode any `Serial` request will cause any outstanding `Parallel` requests will be cancelled with an error message.
* `options.WithRequestProcessIdentifier()`
  * This allows you to control how requests are "identified" or how they will behave (Serial / Parallel)

### Client Options
* `options.OnInitialize` - A delegate that is run right before the client sends the `initialize` request.
  * Also available as an interface `IOnLanguageeClientInitialize`
  * NOTE: This interface can be implemented on `IJsonRpcHandlers`
* `options.OnInitialized` - A delegate that is run right after the client receives the response to the `initialize` request.
  * Also available as an interface `IOnLanguageClientInitialized`
  * NOTE: This interface can be implemented on `IJsonRpcHandlers`
* `options.OnStarted` - A delegate that is run right after the server start process has been completed.
  * Also available as an interface `IOnLanguageClientStarted`
  * NOTE: This interface can be implemented on `IJsonRpcHandlers`
* `options.WithCapability` - Define a capability that will populate and sent to the server.

### Server Options
* `options.OnInitialize` - A delegate that is run right after the client sends the `initialize` request.
  * Also available as an interface `IOnLanguageServerInitialize`
  * NOTE: This interface can be implemented on `IJsonRpcHandlers`
* `options.OnInitialized` - A delegate that is run right before the server sends response to the `initialize` request.
  * Also available as an interface `IOnLanguageServerInitialized`
  * NOTE: This interface can be implemented on `IJsonRpcHandlers`
* `options.OnStarted` - A delegate that is run right after the server start process has been completed.
  * Also available as an interface `IOnLanguageServerStarted`
  * NOTE: This interface can be implemented on `IJsonRpcHandlers`
* `options.ConfigureConfiguration` - Allows you to add your own configuration to the `ILanguageServerConfiguration` object.
* `options.WithConfigurationSection` - Adds a configuration section to be tracked by the server
* `options.WithConfigurationItem` - Adds a configuration item to be tracked by the server.

## Handlers
Handlers can be implemented as classes that implement `IJsonRpcNotificationHandler<>`, `IJsonRpcRequestHandler<>`, `IJsonRpcRequestHandler<,>` or as delegates on the `LanguageServer` itself.

Additionally handlers can be dynamically added and removed after the server has been initialized by using the registry.

`server.Register(registry => {})` will return an `IDisposable` that can be used to remove all the handlers that were registered at that time.

## Protocol Support

Our goal is to abstract the annoying parts of the protocol so you don't have to worry about them.!

### Initialization

Initialization is handled automagically for you so that the server and client start in the correct order.

### Dynamic Registration

The language server protocol supports [registering capabilities dynamically](https://microsoft.github.io/language-server-protocol/specifications/specification-current/#client_registerCapability).

The goal of this library is to make it so you don't have to think about _how_ to registry things dynamically.  You just register your handlers, and things "just work".

### Static vs Dynamic Registration

Some clients support dynamic registration and some do not.  You should not have to worry about what clients do and do not support dynamic registration.

There are some rules when it comes to the language server protocol.

* If you register a capability statically, you cannot register that same capability dynamically. If you do you will be called multiple times (as if you had registered twice).
* If a capability can be dynamically registered, we will never statically register that capability.
  * The server will take of registering any dynamic handlers automatically for you.

### Multiple handlers of the same type
We support the ability to route requests depending on the Registration Options that are defined.

#### Text Document Requests
`TextDocumentRegistrationOptions` can have a `DocumentSelector` that document selector is used to determine where an incoming request will go.

By default duplicate registrations of a given method and `DocumentSelector` is not well defined and should be avoided.  However it is possible to use the `ICanBeIdentifiedHandler` interface to allow registration of multiple handlers of a given method and selector. This is only support for methods that return a collection in some form.

#### Resolve Requests
`ICompletionHandler`, `ICodeLensHandler` and `IDocumentLinkHandler` support multiple handlers by default, and will also ensure that resolve requests will get routed back to the handler that created the item in the first place.  However note ordering of the results is not guaranteed, and may not be desired.

#### Execute Command
Execute command is a special method, that can be used by the client to do some work on the server side.  The commands generally come from the server in some way (code action, code lens, completion, etc).

The server will correctly route command requests to the handler that implements the command. And we also have support for strongly typed command handlers where we will deserialize the arguments array for you.

### Custom Handlers / Capabilities

The protocol defines the ability to have custom extensions, that may not be supported by all clients but allows for greater flexibility in implementations.

It is possible to create custom handlers and capabilities then have them be consumed by handlers and provided back to the client as options.

A custom method and handler can be made by just using the correct attributes.  Handlers can be interfaces or classes, the important part is the type that is implemented.

NOTE: This is where `WithAssembly` may be important, you will want to ensure your assembly that has the custom handlers in it is added to the `options` of your server / client.

```csharp
    [Parallel, Method("tests/run")]
    public class UnitTest : IRequest
    {
        public string Name { get; set; }
    }

    [Parallel, Method("tests/discover")]
    public interface IDiscoverUnitTestsHandler : IJsonRpcRequestHandler<DiscoverUnitTestsParams, Container<UnitTest>>,
        IRegistration<UnitTestRegistrationOptions>,
        ICapability<UnitTestCapability>
    {
    }
```

In order for capabilities (client options to the server) and static options (static registration options for the client) to flow through you must create the correct classes.

The converter must be defined to ensure that the correct data is provided to the client. It does not need to be public, it will get scanned out of the assembly.

NOTE: The static options will only be created when the capability cannot be registered dynamically.

```csharp
    // each key is a json segment of the ClientCapabilities object
    [CapabilityKey("workspace", "unitTests")] object
    public class UnitTestCapability : DynamicCapability
    {
        public string Property { get; set; }
    }

    public class UnitTestRegistrationOptions : IWorkDoneProgressOptions, IRegistrationOptions
    {
        [Optional] public bool SupportsDebugging { get; set; }
        [Optional] public bool WorkDoneProgress { get; set; }

        public class StaticOptions : WorkDoneProgressOptions
        {
            [Optional] public bool SupportsDebugging { get; set; }
        }

        class Converter : RegistrationOptionsConverterBase<UnitTestRegistrationOptions, StaticOptions>
        {
            // This is the key where the options will show up on the ServerCapabilities object
            public Converter() : base("unitTests")
            {
            }

            public override StaticOptions Convert(UnitTestRegistrationOptions source)
            {
                return new StaticOptions {
                    SupportsDebugging = source.SupportsDebugging,
                    WorkDoneProgress = source.WorkDoneProgress,
                };
            }
        }
    }
```

### Configuration
The `LanguageSever` wraps the protocol `workspace/didChangeConfiguration` and `workspace/configuration` methods for you and exposes `ILanguageServerConfiguration` which also implements `Microsoft.Extensions.Configuration.IConfiguration`.

Configuration sections and configuration items can be added and removed at anytime and will query the data from the client.

You can also query stateless configuration using `GetConfiguration` to look at some configuration from the client.

You can also get scoped configuration, which will give you the configuration in the scope of a given `DocumentUri`.

NOTE: Scoped configuration must be disposed otherwise it will continue to be updated.

### Injecting `ILanguageServer` / `ILanguageClient`
You cannot inject `ILanguageServer` or `ILanguageClient` in your handlers (or their services!) because handlers are resolved as part of their initialization.  However you can inject `ILanguageServerFacade` or `ILanguageClientFacade`.  The should have all the information you're looking for from the core types.

## Language Proposals
The protocol periodically goes through revisions to add new features and properties to the specification.  We publish these proposals in the Proposals nuget package.  Due to the nature of these proposals they can/will change regularly, do not expect these to be stable until the next version of the spec is released.  Our goal is for full fidelity and to ensure these proposals can be tested and vetted as thoroughly as possible.

By registering a proposed feature as a handler it should enable itself with any significant changes.  However you can call the `EnableProposals()` method on the server or client options to ensure that the proposed Capabilities are serialized as the corresponding capabilities object.  This means you can cast `WorkspaceClientCapabilities` as `ProposedWorkspaceClientCapabilities` safely and see the capabilities that may be provided by the client or server.
