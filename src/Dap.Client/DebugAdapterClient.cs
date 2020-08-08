﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DryIoc;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Extensions.DebugAdapter.Protocol;
using OmniSharp.Extensions.DebugAdapter.Protocol.Client;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using OmniSharp.Extensions.DebugAdapter.Shared;
using OmniSharp.Extensions.JsonRpc;
using IOutputHandler = OmniSharp.Extensions.JsonRpc.IOutputHandler;
using OutputHandler = OmniSharp.Extensions.JsonRpc.OutputHandler;

namespace OmniSharp.Extensions.DebugAdapter.Client
{
    public class DebugAdapterClient : JsonRpcServerBase, IDebugAdapterClient, IDebugAdapterInitializedHandler
    {
        private readonly DebugAdapterSettingsBag _settingsBag;
        private readonly DebugAdapterHandlerCollection _collection;
        private readonly IEnumerable<OnDebugAdapterClientInitializeDelegate> _initializeDelegates;
        private readonly IEnumerable<IOnDebugAdapterClientInitialize> _initializeHandlers;
        private readonly IEnumerable<OnDebugAdapterClientInitializedDelegate> _initializedDelegates;
        private readonly IEnumerable<IOnDebugAdapterClientInitialized> _initializedHandlers;
        private readonly IEnumerable<OnDebugAdapterClientStartedDelegate> _startedDelegates;
        private readonly IEnumerable<IOnDebugAdapterClientStarted> _startedHandlers;
        private readonly CompositeDisposable _disposable = new CompositeDisposable();
        private readonly Connection _connection;
        private readonly DapReceiver _receiver;
        private readonly IServiceProvider _serviceProvider;
        private readonly ISubject<InitializedEvent> _initializedComplete = new AsyncSubject<InitializedEvent>();
        private bool _started;
        private readonly int? _concurrency;

        internal static IContainer CreateContainer(DebugAdapterClientOptions options, IServiceProvider outerServiceProvider) =>
            JsonRpcServerContainer.Create(outerServiceProvider)
                .AddDebugAdapterClientInternals(options, outerServiceProvider);

        public static DebugAdapterClient Create(DebugAdapterClientOptions options) => Create(options, null);
        public static DebugAdapterClient Create(Action<DebugAdapterClientOptions> optionsAction) => Create(optionsAction, null);
        public static DebugAdapterClient Create(Action<DebugAdapterClientOptions> optionsAction, IServiceProvider outerServiceProvider)
        {
            var options = new DebugAdapterClientOptions();
            optionsAction(options);
            return Create(options, outerServiceProvider);
        }

        public static DebugAdapterClient Create(DebugAdapterClientOptions options, IServiceProvider outerServiceProvider) => CreateContainer(options, outerServiceProvider).Resolve<DebugAdapterClient>();

        public static Task<DebugAdapterClient> From(DebugAdapterClientOptions options) => From(options, null, CancellationToken.None);
        public static Task<DebugAdapterClient> From(Action<DebugAdapterClientOptions> optionsAction) => From(optionsAction, null, CancellationToken.None);
        public static Task<DebugAdapterClient> From(DebugAdapterClientOptions options, CancellationToken cancellationToken) => From(options, null, cancellationToken);
        public static Task<DebugAdapterClient> From(Action<DebugAdapterClientOptions> optionsAction, CancellationToken cancellationToken) => From(optionsAction, null, cancellationToken);
        public static Task<DebugAdapterClient> From(DebugAdapterClientOptions options, IServiceProvider outerServiceProvider) => From(options, outerServiceProvider, CancellationToken.None);
        public static Task<DebugAdapterClient> From(Action<DebugAdapterClientOptions> optionsAction, IServiceProvider outerServiceProvider) => From(optionsAction, outerServiceProvider, CancellationToken.None);
        public static Task<DebugAdapterClient> From(Action<DebugAdapterClientOptions> optionsAction, IServiceProvider outerServiceProvider, CancellationToken cancellationToken)
        {
            var options = new DebugAdapterClientOptions();
            optionsAction(options);
            return From(options, outerServiceProvider, cancellationToken);
        }

        public static async Task<DebugAdapterClient> From(DebugAdapterClientOptions options, IServiceProvider outerServiceProvider, CancellationToken cancellationToken)
        {
            var server = Create(options, outerServiceProvider);
            await server.Initialize(cancellationToken);
            return server;
        }

        internal DebugAdapterClient(
            IOptions<DebugAdapterClientOptions> options,
            InitializeRequestArguments clientSettings,
            DebugAdapterSettingsBag settingsBag,
            DebugAdapterHandlerCollection collection,
            IEnumerable<OnDebugAdapterClientStartedDelegate> onClientStartedDelegates,
            DapReceiver receiver,
            IResponseRouter responseRouter,
            IServiceProvider serviceProvider,
            IDebugAdapterClientProgressManager debugAdapterClientProgressManager,
            Connection connection,
            IEnumerable<OnDebugAdapterClientInitializeDelegate> initializeDelegates,
            IEnumerable<IOnDebugAdapterClientInitialize> initializeHandlers,
            IEnumerable<OnDebugAdapterClientInitializedDelegate> initializedDelegates,
            IEnumerable<IOnDebugAdapterClientInitialized> initializedHandlers,
            IEnumerable<IOnDebugAdapterClientStarted> startedHandlers) : base(collection, responseRouter)
        {
            _settingsBag = settingsBag;
            ClientSettings = clientSettings;
            _collection = collection;
            _startedDelegates = onClientStartedDelegates;
            _receiver = receiver;
            _serviceProvider = serviceProvider;
            ProgressManager = debugAdapterClientProgressManager;
            _connection = connection;
            _initializeDelegates = initializeDelegates;
            _initializeHandlers = initializeHandlers;
            _initializedDelegates = initializedDelegates;
            _initializedHandlers = initializedHandlers;
            _startedHandlers = startedHandlers;
            _concurrency = options.Value.Concurrency;

            _disposable.Add(collection.Add(this));
        }

        public async Task Initialize(CancellationToken token)
        {
            await DebugAdapterEventingHelper.Run(
                _initializeDelegates,
                (handler, ct) => handler(this, ClientSettings, ct),
                _initializeHandlers.Union(_collection.Select(z => z.Handler).OfType<IOnDebugAdapterClientInitialize>()),
                (handler, ct) => handler.OnInitialize(this, ClientSettings, ct),
                _concurrency,
                token
            );

            RegisterCapabilities(ClientSettings);

            _connection.Open();
            var serverParams = await this.RequestDebugAdapterInitialize(ClientSettings, token);

            ServerSettings = serverParams;
            _receiver.Initialized();

            await DebugAdapterEventingHelper.Run(
                _initializedDelegates,
                (handler, ct) => handler(this, ClientSettings, ServerSettings, ct),
                _initializedHandlers.Union(_collection.Select(z => z.Handler).OfType<IOnDebugAdapterClientInitialized>()),
                (handler, ct) => handler.OnInitialized(this, ClientSettings, ServerSettings, ct),
                _concurrency,
                token
            );

            await _initializedComplete.ToTask(token);

            await DebugAdapterEventingHelper.Run(
                _startedDelegates,
                (handler, ct) => handler(this, ct),
                _startedHandlers.Union(_collection.Select(z => z.Handler).OfType<IOnDebugAdapterClientStarted>()),
                (handler, ct) => handler.OnStarted(this, ct),
                _concurrency,
                token
            );
            _started = true;
        }

        async Task<Unit> IRequestHandler<InitializedEvent, Unit>.Handle(InitializedEvent request, CancellationToken cancellationToken)
        {
            await DebugAdapterEventingHelper.Run(
                _initializedDelegates,
                (handler, ct) => handler(this, ClientSettings, ServerSettings, ct),
                _initializedHandlers.Union(_collection.Select(z => z.Handler).OfType<IOnDebugAdapterClientInitialized>()),
                (handler, ct) => handler.OnInitialized(this, ClientSettings, ServerSettings, ct),
                _concurrency,
                cancellationToken
            );

            _initializedComplete.OnNext(request);
            _initializedComplete.OnCompleted();
            return Unit.Value;
        }

        private void RegisterCapabilities(InitializeRequestArguments capabilities)
        {
            capabilities.SupportsRunInTerminalRequest ??= _collection.ContainsHandler(typeof(IRunInTerminalHandler));
            capabilities.SupportsProgressReporting ??= _collection.ContainsHandler(typeof(IProgressStartHandler)) &&
                                                       _collection.ContainsHandler(typeof(IProgressUpdateHandler)) &&
                                                       _collection.ContainsHandler(typeof(IProgressEndHandler));
        }

        public InitializeRequestArguments ClientSettings
        {
            get => _settingsBag.ClientSettings;
            private set => _settingsBag.ClientSettings = value;
        }

        public InitializeResponse ServerSettings
        {
            get => _settingsBag.ServerSettings;
            private set => _settingsBag.ServerSettings = value;
        }
        public IDebugAdapterClientProgressManager ProgressManager { get; }

        public void Dispose()
        {
            _disposable?.Dispose();
            _connection?.Dispose();
        }

        object IServiceProvider.GetService(Type serviceType) => _serviceProvider.GetService(serviceType);

        public IDisposable Register(Action<IDebugAdapterClientRegistry> registryAction)
        {
            var manager = new CompositeHandlersManager(_collection);
            registryAction(new DebugAdapterClientRegistry(manager));

            var result = manager.GetDisposable();
            if (_started)
            {
                static IEnumerable<T> GetUniqueHandlers<T>(CompositeDisposable disposable)
                {
                    return disposable.OfType<IHandlerDescriptor>()
                        .Select(z => z.Handler)
                        .OfType<T>()
                        .Concat(disposable.OfType<CompositeDisposable>().SelectMany(GetUniqueHandlers<T>))
                        .Distinct();
                }

                Observable.Concat(
                    GetUniqueHandlers<IOnDebugAdapterClientInitialize>(result)
                        .Select(handler => Observable.FromAsync((ct) => handler.OnInitialize(this, ClientSettings, ct)))
                        .Merge(),
                    GetUniqueHandlers<IOnDebugAdapterClientInitialized>(result)
                        .Select(handler => Observable.FromAsync((ct) => handler.OnInitialized(this, ClientSettings, ServerSettings, ct)))
                        .Merge(),
                    GetUniqueHandlers<IOnDebugAdapterClientStarted>(result)
                        .Select(handler => Observable.FromAsync((ct) => handler.OnStarted(this, ct)))
                        .Merge()
                ).Subscribe();
            }

            return result;
        }
    }
}
