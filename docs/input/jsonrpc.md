Title: JSON RPC
ShowInSidebar: true
Order: 2
---

# Using Json Rpc Standalone
One of the benefits of the [JSON-RPC protocol](https://www.jsonrpc.org/specification) is that communication can be fully bi-directional.  Through some sort of interface agreement, like LSP / DAP, both sides can act as both client (sender) and server (reciever).

# Creating a JsonRpcServer
`JsonRpcServer` can be created through two methods.

## Standalone

> `JsonRpcServer.Create(options => {})`
This will create a server where you provide options, handlers and more.  An optional `IServiceProvider` can be provided that will be used as a fallback container when `IJsonRcpHandlers` are being resolved.

## Microsoft.Extensions.DependencyInjection

> `services.AddJsonRpcServer([string name, ], options => {})`
This will add `JsonRpcServer` to your service collection, or any number of named `JsonRpcServer`s.

* In the event that you add multiple named servers, they must be resolved using `JsonRpcServerResolver`.

When created through Microsoft DI the server will use the `IServiceProvider` as a fallback when resolving `IJsonRpcHandlers`.

## Options

Some of the important options include...

* `options.WithInput()` takes an input `Stream` or `PipeReader`
* `options.WithOutput()` takes an output `Stream` or `PipeWriter`
* `options.WithAssemblies()` takes additional assemblies that will participate in scanning operations.
  * Sometimes we scan this list of assemblies for potential strongly typed requests and notifications
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

## Testing

We have some helper classes that can be used to aid in Unit Testing such as `JsonRpcServerTestBase`.

## Handlers
Handlers can be implemented as classes that implement `IJsonRpcNotificationHandler<>`, `IJsonRpcRequestHandler<>`, `IJsonRpcRequestHandler<,>` or as delegates on the `JsonRpcServer` itself.

Additionally handlers can be dynamically added and removed after the server has been initialized by using the registry.

`server.Register(registry => {})` will return an `IDisposable` that can be used to remove all the handlers that were registered at that time.
