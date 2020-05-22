using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Reactive.Disposables;

namespace OmniSharp.Extensions.JsonRpc
{

    public class JsonRpcServer : IJsonRpcServer
    {
        private readonly Connection _connection;
        private readonly IRequestRouter<IHandlerDescriptor> _requestRouter;
        private readonly IReceiver _receiver;
        private readonly ISerializer _serializer;
        private readonly HandlerCollection _collection;
        private readonly List<(string method, Func<IServiceProvider, IJsonRpcHandler>)> _namedHandlers = new List<(string method, Func<IServiceProvider, IJsonRpcHandler>)>();
        private readonly IResponseRouter _responseRouter;
        private readonly IServiceProvider _serviceProvider;

        public static Task<IJsonRpcServer> From(Action<JsonRpcServerOptions> optionsAction)
        {
            var options = new JsonRpcServerOptions();
            optionsAction(options);
            return From(options);
        }

        public static async Task<IJsonRpcServer> From(JsonRpcServerOptions options)
        {
            var server = new JsonRpcServer(
                options.Input,
                options.Output,
                options.Receiver,
                options.RequestProcessIdentifier,
                options.LoggerFactory,
                options.Serializer,
                options.Services,
                options.HandlerTypes.Select(x => x.Assembly)
                    .Distinct().Concat(options.HandlerAssemblies),
                options.Handlers,
                options.NamedHandlers,
                options.NamedServiceHandlers,
                options.Concurrency
            );

            await server.Initialize();

            return server;
        }

        internal JsonRpcServer(
            Stream input,
            Stream output,
            IReceiver receiver,
            IRequestProcessIdentifier requestProcessIdentifier,
            ILoggerFactory loggerFactory,
            ISerializer serializer,
            IServiceCollection services,
            IEnumerable<Assembly> assemblies,
            IEnumerable<IJsonRpcHandler> handlers,
            IEnumerable<(string name, IJsonRpcHandler handler)> namedHandlers,
            IEnumerable<(string name, Func<IServiceProvider, IJsonRpcHandler> handlerFunc)> namedServiceHandlers,
            int? concurrency)
        {
            var outputHandler = new OutputHandler(output, serializer, loggerFactory.CreateLogger<OutputHandler>());

            services.AddLogging();
            _receiver = receiver;
            _serializer = serializer;
            _collection = new HandlerCollection();

            services.AddSingleton<IOutputHandler>(outputHandler);
            services.AddSingleton(_collection);
            services.AddSingleton(_serializer);
            services.AddSingleton<OmniSharp.Extensions.JsonRpc.ISerializer>(_serializer);
            services.AddSingleton(requestProcessIdentifier);
            services.AddSingleton(_receiver);
            services.AddSingleton(loggerFactory);

            services.AddJsonRpcMediatR(assemblies);
            services.AddSingleton<IJsonRpcServer>(this);
            services.AddSingleton<IRequestRouter<IHandlerDescriptor>, RequestRouter>();
            services.AddSingleton<IResponseRouter, ResponseRouter>();

            var foundHandlers = services
                .Where(x => typeof(IJsonRpcHandler).IsAssignableFrom(x.ServiceType) && x.ServiceType != typeof(IJsonRpcHandler))
                .ToArray();

            // Handlers are created at the start and maintained as a singleton
            foreach (var handler in foundHandlers)
            {
                services.Remove(handler);

                if (handler.ImplementationFactory != null)
                    services.Add(ServiceDescriptor.Singleton(typeof(IJsonRpcHandler), handler.ImplementationFactory));
                else if (handler.ImplementationInstance != null)
                    services.Add(ServiceDescriptor.Singleton(typeof(IJsonRpcHandler), handler.ImplementationInstance));
                else
                    services.Add(ServiceDescriptor.Singleton(typeof(IJsonRpcHandler), handler.ImplementationType));
            }

            _serviceProvider = services.BuildServiceProvider();

            var serviceHandlers = _serviceProvider.GetServices<IJsonRpcHandler>().ToArray();
            _collection.Add(serviceHandlers);
            _collection.Add(handlers.ToArray());
            foreach (var (name, handler) in namedHandlers)
            {
            _collection.Add(name, handler);
            }
            foreach (var (name, handlerFunc) in namedServiceHandlers)
            {
                _collection.Add(name, handlerFunc(_serviceProvider));
            }

            _requestRouter = _serviceProvider.GetRequiredService<IRequestRouter<IHandlerDescriptor>>();
            _collection.Add(new CancelRequestHandler<IHandlerDescriptor>(_requestRouter));
            _responseRouter = _serviceProvider.GetRequiredService<IResponseRouter>();
            _connection = new Connection(
                input,
                outputHandler,
                receiver,
                requestProcessIdentifier,
                _requestRouter,
                _responseRouter,
                loggerFactory,
                serializer,
                concurrency
            );
        }

        public IDisposable AddHandler(string method, IJsonRpcHandler handler)
        {
            return _collection.Add(method, handler);
        }

        public IDisposable AddHandler(string method, Func<IServiceProvider, IJsonRpcHandler> handlerFunc)
        {
            _namedHandlers.Add((method, handlerFunc));
            return Disposable.Empty;
        }

        public IDisposable AddHandlers(params IJsonRpcHandler[] handlers)
        {
            return _collection.Add(handlers);
        }

        public IDisposable AddHandler(string method, Type handlerType)
        {
            return _collection.Add(method, ActivatorUtilities.CreateInstance(_serviceProvider, handlerType) as IJsonRpcHandler);
        }

        public IDisposable AddHandler<T>()
            where T : IJsonRpcHandler
        {
            return AddHandlers(typeof(T));
        }

        public IDisposable AddHandlers(params Type[] handlerTypes)
        {
            return _collection.Add(
                handlerTypes
                .Select(handlerType => ActivatorUtilities.CreateInstance(_serviceProvider, handlerType) as IJsonRpcHandler)
                .ToArray());
        }

        private async Task Initialize()
        {
            await Task.Yield();
            _connection.Open();
        }

        public void SendNotification(string method)
        {
            _responseRouter.SendNotification(method);
        }

        public void SendNotification<T>(string method, T @params)
        {
            _responseRouter.SendNotification(method, @params);
        }

        public void SendNotification(IRequest @params)
        {
            _responseRouter.SendNotification(@params);
        }

        public Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> @params, CancellationToken cancellationToken)
        {
            return _responseRouter.SendRequest(@params, cancellationToken);
        }

        public IResponseRouterReturns SendRequest<T>(string method, T @params)
        {
            return _responseRouter.SendRequest<T>(method, @params);
        }

        public IResponseRouterReturns SendRequest(string method)
        {
            return _responseRouter.SendRequest(method);
        }

        public TaskCompletionSource<JToken> GetRequest(long id)
        {
            return _responseRouter.GetRequest(id);
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
