using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server.Abstractions;
using OmniSharp.Extensions.LanguageServer.Server.Matchers;
using OmniSharp.Extensions.LanguageServer.Server.Pipelines;
using ISerializer = OmniSharp.Extensions.LanguageServer.Protocol.Serialization.ISerializer;
using System.Reactive.Disposables;
using DryIoc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.General;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;
using OmniSharp.Extensions.LanguageServer.Protocol.Progress;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;
using OmniSharp.Extensions.LanguageServer.Protocol.Shared;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Extensions.LanguageServer.Server.Configuration;
using OmniSharp.Extensions.LanguageServer.Server.Logging;
using OmniSharp.Extensions.LanguageServer.Shared;
using OmniSharp.Extensions.JsonRpc.DryIoc;

namespace OmniSharp.Extensions.LanguageServer.Server
{
    public static class LanguageServerServiceCollectionExtensions
    {
        internal static IContainer AddLanguageServerInternals(this IContainer container, LanguageServerOptions options, IServiceProvider outerServiceProvider)
        {
            if (options.Receiver == null)
            {
                throw new ArgumentException("Receiver is missing!", nameof(options));
            }

            container  = container.AddLanguageProtocolInternals(options);

            container.RegisterInstanceMany(options.Receiver);
            if (options.OnUnhandledException != null)
            {
                container.RegisterInstance(options.OnUnhandledException);
            }
            else
            {
                container.RegisterDelegate(_ => new OnUnhandledExceptionHandler(e => { _.GetRequiredService<LanguageServer>().ForcefulShutdown(); }));
            }

            container.RegisterMany<TextDocumentLanguageServer>(serviceTypeCondition: type => type.Name.Contains(nameof(TextDocumentLanguageServer)));
            container.RegisterMany<ClientLanguageServer>(serviceTypeCondition: type => type.Name.Contains(nameof(ClientLanguageServer)));
            container.RegisterMany<GeneralLanguageServer>(serviceTypeCondition: type => type.Name.Contains(nameof(GeneralLanguageServer)));
            container.RegisterMany<WindowLanguageServer>(serviceTypeCondition: type => type.Name.Contains(nameof(WindowLanguageServer)));
            container.RegisterMany<WorkspaceLanguageServer>(serviceTypeCondition: type => type.Name.Contains(nameof(WorkspaceLanguageServer)));
            container.RegisterInstance<IOptionsFactory<LanguageServerOptions>>(new ValueOptionsFactory<LanguageServerOptions>(options));

            container.RegisterMany<LanguageServer>(serviceTypeCondition: type => type == typeof(ILanguageServer) || type == typeof(LanguageServer)/*, reuse: Reuse.Singleton*/);
            container.RegisterInitializer<LanguageServer>((server, context) => {
                var manager = context.Resolve<IHandlersManager>();
                var descriptions = context.Resolve<IJsonRpcHandlerCollection>();
                descriptions.Populate(context, manager);

                var handlers = context.ResolveMany<IJsonRpcHandler>();
                foreach (var handler in handlers)
                {
                    manager.Add(handler, new JsonRpcHandlerOptions());
                }
            });

            container.RegisterMany<DidChangeConfigurationProvider>(
                made: Parameters.Of
                    .Type<Action<IConfigurationBuilder>>(defaultValue: options.ConfigurationBuilderAction)
            );

            // container.
            var providedConfiguration = options.Services.FirstOrDefault(z => z.ServiceType == typeof(IConfiguration) && z.ImplementationInstance is IConfiguration);
            container.RegisterDelegate<IConfiguration>(_ => {
                var builder = new ConfigurationBuilder();
                var didChangeConfigurationProvider = _.GetRequiredService<DidChangeConfigurationProvider>();
                if (outerServiceProvider != null)
                {
                    var outerConfiguration = outerServiceProvider.GetService<IConfiguration>();
                    if (outerConfiguration != null)
                    {
                        builder.AddConfiguration(outerConfiguration, false);
                    }
                }

                if (providedConfiguration != null)
                {
                    builder.AddConfiguration(providedConfiguration.ImplementationInstance as IConfiguration);
                }

                return builder.AddConfiguration(didChangeConfigurationProvider).Build();
            });

            container.RegisterMany<LanguageServerLoggerFilterOptions>(serviceTypeCondition: type => type.IsInterface);
            container.RegisterInstance(options.ServerInfo ?? new ServerInfo() {
                Name = Assembly.GetEntryAssembly()?.GetName().Name,
                Version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                              ?.InformationalVersion ??
                          Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyVersionAttribute>()?.Version
            });

            container.RegisterMany<TextDocumentMatcher>();
            container.RegisterMany<ExecuteCommandMatcher>();
            container.RegisterMany<ResolveCommandMatcher>();
            container.RegisterMany(new [] { typeof(ResolveCommandPipeline<,>) });
            container.RegisterMany<ServerWorkDoneManager>();

            return container;
        }

        public static IServiceCollection AddLanguageServer(this IServiceCollection services, Action<LanguageServerOptions> configureOptions = null)
        {
            return AddLanguageServer(services, Options.DefaultName, configureOptions);
        }

        public static IServiceCollection AddLanguageServer(this IServiceCollection services, string name, Action<LanguageServerOptions> configureOptions = null)
        {
            // If we get called multiple times we're going to remove the default server
            // and force consumers to use the resolver.
            if (services.Any(d => d.ServiceType == typeof(LanguageServer) || d.ServiceType == typeof(ILanguageServer)))
            {
                services.RemoveAll<LanguageServer>();
                services.RemoveAll<ILanguageServer>();
                services.AddSingleton<ILanguageServer>(_ =>
                    throw new NotSupportedException("LanguageServer has been registered multiple times, you must use LanguageServer instead"));
                services.AddSingleton<LanguageServer>(_ =>
                    throw new NotSupportedException("LanguageServer has been registered multiple times, you must use LanguageServer instead"));
            }

            services
                .AddOptions()
                .AddLogging();
            services.TryAddSingleton<LanguageServerResolver>();
            services.TryAddSingleton(_ => _.GetRequiredService<LanguageServerResolver>().Get(name));
            services.TryAddSingleton<ILanguageServer>(_ => _.GetRequiredService<LanguageServerResolver>().Get(name));

            if (configureOptions != null)
            {
                services.Configure(name, configureOptions);
            }

            return services;
        }
    }

    public class LanguageServerResolver : IDisposable
    {
        private readonly IOptionsMonitor<LanguageServerOptions> _monitor;
        private readonly IServiceProvider _outerServiceProvider;
        private readonly ConcurrentDictionary<string, LanguageServer> _servers = new ConcurrentDictionary<string, LanguageServer>();

        public LanguageServerResolver(IOptionsMonitor<LanguageServerOptions> monitor, IServiceProvider outerServiceProvider)
        {
            _monitor = monitor;
            _outerServiceProvider = outerServiceProvider;
        }

        public LanguageServer Get(string name)
        {
            if (_servers.TryGetValue(name, out var server)) return server;

            var options = name == Options.DefaultName ? _monitor.CurrentValue : _monitor.Get(name);

            var container = LanguageServer.CreateContainer(options, _outerServiceProvider);
            server = container.Resolve<LanguageServer>();
            _servers.TryAdd(name, server);

            return server;
        }

        public void Dispose()
        {
            foreach (var item in _servers.Values) item.Dispose();
        }
    }

    public partial class LanguageServer : JsonRpcServerBase, ILanguageServer, ILanguageProtocolInitializeHandler, ILanguageProtocolInitializedHandler, IAwaitableTermination,
        IDisposable
    {
        private readonly Connection _connection;
        private ClientVersion? _clientVersion;
        private readonly ServerInfo _serverInfo;
        private readonly IServerWorkDoneManager _serverWorkDoneManager;
        private readonly ILspServerReceiver _serverReceiver;
        private readonly ISerializer _serializer;
        private readonly TextDocumentIdentifiers _textDocumentIdentifiers;
        private readonly IHandlerCollection _collection;
        private readonly IEnumerable<InitializeDelegate> _initializeDelegates;
        private readonly IEnumerable<InitializedDelegate> _initializedDelegates;
        private readonly IEnumerable<OnServerStartedDelegate> _startedDelegates;
        private readonly IResponseRouter _responseRouter;
        private readonly ISubject<InitializeResult> _initializeComplete = new AsyncSubject<InitializeResult>();
        private readonly CompositeDisposable _disposable = new CompositeDisposable();
        private readonly ISupportedCapabilities _supportedCapabilities;
        private Task _initializingTask;
        private readonly LanguageProtocolSettingsBag _settingsBag;
        private readonly IServiceProvider _fallbackServiceProvider;

        internal static IContainer CreateContainer(LanguageServerOptions options, IServiceProvider outerServiceProvider) =>
            JsonRpcServerContainer.Create(outerServiceProvider)
                .AddLanguageServerInternals(options, outerServiceProvider)
                .Populate(options.Services);

        public static LanguageServer Create(LanguageServerOptions options) => Create(options, null);
        public static LanguageServer Create(Action<LanguageServerOptions> optionsAction) => Create(optionsAction, null);

        public static LanguageServer Create(Action<LanguageServerOptions> optionsAction, IServiceProvider outerServiceProvider)
        {
            var options = new LanguageServerOptions();
            optionsAction(options);
            return Create(options, outerServiceProvider);
        }

        public static LanguageServer Create(LanguageServerOptions options, IServiceProvider outerServiceProvider) => CreateContainer(options, outerServiceProvider).Resolve<LanguageServer>();
        public static Task<LanguageServer> From(LanguageServerOptions options) => From(options, null, CancellationToken.None);
        public static Task<LanguageServer> From(Action<LanguageServerOptions> optionsAction) => From(optionsAction, null, CancellationToken.None);
        public static Task<LanguageServer> From(LanguageServerOptions options, CancellationToken cancellationToken) => From(options, null, cancellationToken);
        public static Task<LanguageServer> From(Action<LanguageServerOptions> optionsAction, CancellationToken cancellationToken) => From(optionsAction, null, cancellationToken);

        public static Task<LanguageServer> From(LanguageServerOptions options, IServiceProvider outerServiceProvider) =>
            From(options, outerServiceProvider, CancellationToken.None);

        public static Task<LanguageServer> From(Action<LanguageServerOptions> optionsAction, IServiceProvider outerServiceProvider) =>
            From(optionsAction, outerServiceProvider, CancellationToken.None);

        public static Task<LanguageServer> From(Action<LanguageServerOptions> optionsAction, IServiceProvider outerServiceProvider, CancellationToken cancellationToken)
        {
            var options = new LanguageServerOptions();
            optionsAction(options);
            return From(options, outerServiceProvider, cancellationToken);
        }

        public static async Task<LanguageServer> From(LanguageServerOptions options, IServiceProvider outerServiceProvider, CancellationToken cancellationToken)
        {
            var server = Create(options, outerServiceProvider);
            await server.Initialize(cancellationToken);
            return server;
        }

        /// <summary>
        /// Create the server without connecting to the client
        ///
        /// Mainly used for unit testing
        /// </summary>
        /// <param name="optionsAction"></param>
        /// <returns></returns>
        public static LanguageServer PreInit(Action<LanguageServerOptions> optionsAction)
        {
            return Create(optionsAction);
        }

        /// <summary>
        /// Create the server without connecting to the client
        ///
        /// Mainly used for unit testing
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static LanguageServer PreInit(LanguageServerOptions options)
        {
            return Create(options);
        }

        internal LanguageServer(
            Connection connection,
            IResponseRouter responseRouter,
            IOptions<LanguageServerOptions> options,
            ILanguageServerConfiguration configuration,
            ServerInfo serverInfo,
            ILspServerReceiver receiver,
            ISerializer serializer,
            IServiceProvider serviceProvider,
            ISupportedCapabilities supportedCapabilities,
            TextDocumentIdentifiers textDocumentIdentifiers,
            IEnumerable<InitializeDelegate> initializeDelegates,
            IEnumerable<InitializedDelegate> initializedDelegates,
            IEnumerable<OnServerStartedDelegate> startedDelegates,
            IServiceProvider fallbackServiceProvider,
            IServerWorkDoneManager serverWorkDoneManager,
            ITextDocumentLanguageServer textDocumentLanguageServer,
            IClientLanguageServer clientLanguageServer,
            IGeneralLanguageServer generalLanguageServer,
            IWindowLanguageServer windowLanguageServer,
            IWorkspaceLanguageServer workspaceLanguageServer,
            LanguageProtocolSettingsBag languageProtocolSettingsBag,
            SharedHandlerCollection handlerCollection
        ) : base(options.Value)
        {
            Configuration = configuration;

            _responseRouter = responseRouter;
            _connection = connection;
            _serverInfo = serverInfo;
            _serverReceiver = receiver;
            _serializer = serializer;
            _supportedCapabilities = supportedCapabilities;
            _textDocumentIdentifiers = textDocumentIdentifiers;
            _initializeDelegates = initializeDelegates;
            _initializedDelegates = initializedDelegates;
            _startedDelegates = startedDelegates;
            _serverWorkDoneManager = serverWorkDoneManager;
            _settingsBag = languageProtocolSettingsBag;
            Services = serviceProvider;
            _fallbackServiceProvider = fallbackServiceProvider;
            _collection = handlerCollection;

            // We need to at least create Window here in case any handler does logging in their constructor
            TextDocument = textDocumentLanguageServer;
            Client = clientLanguageServer;
            General = generalLanguageServer;
            Window = windowLanguageServer;
            Workspace = workspaceLanguageServer;

            _disposable.Add(_collection.Add(this));

            var serviceIdentifiers = serviceProvider.GetServices<ITextDocumentIdentifier>().ToArray();
            _disposable.Add(_textDocumentIdentifiers.Add(serviceIdentifiers));
        }


        public ITextDocumentLanguageServer TextDocument { get; }
        public IClientLanguageServer Client { get; }
        public IGeneralLanguageServer General { get; }
        public IWindowLanguageServer Window { get; }
        public IWorkspaceLanguageServer Workspace { get; }

        public InitializeParams ClientSettings
        {
            get => _settingsBag.ClientSettings;
            private set => _settingsBag.ClientSettings = value;
        }

        public InitializeResult ServerSettings
        {
            get => _settingsBag.ServerSettings;
            private set => _settingsBag.ServerSettings = value;
        }

        public IServiceProvider Services { get; }
        public IProgressManager ProgressManager { get; }

        public IServerWorkDoneManager WorkDoneManager => _serverWorkDoneManager;
        public ILanguageServerConfiguration Configuration { get; }

        public async Task Initialize(CancellationToken token)
        {
            if (_initializingTask != null)
            {
                try
                {
                    await _initializingTask;
                }
                catch
                {
                    // Swallow exceptions because the original initialization task will report errors if it fails (don't want to doubly report).
                }

                return;
            }

            _connection.Open();
            try
            {
                _initializingTask = _initializeComplete
                    .Select(result => _startedDelegates.Select(@delegate =>
                            Observable.FromAsync(() => @delegate(this, result, token))
                        )
                        .ToObservable()
                        .Merge()
                        .Select(z => result)
                    )
                    .Merge()
                    .LastOrDefaultAsync()
                    .ToTask(token);
                await _initializingTask;
            }
            catch (TaskCanceledException e)
            {
                _initializeComplete.OnError(e);
                throw;
            }
            catch (Exception e)
            {
                _initializeComplete.OnError(e);
                throw;
            }
        }

        async Task<InitializeResult> IRequestHandler<InitializeParams, InitializeResult>.Handle(
            InitializeParams request, CancellationToken token)
        {
            ClientSettings = request;

            if (request.Trace == InitializeTrace.Verbose)
            {
                var loggerSettings = Services.GetService<LanguageServerLoggerSettings>();

                if (loggerSettings?.MinimumLogLevel <= LogLevel.Information)
                {
                    loggerSettings.MinimumLogLevel = LogLevel.Trace;
                }

                var optionsMonitor =
                    Services.GetService<IOptionsMonitor<LoggerFilterOptions>>() as
                        LanguageServerLoggerFilterOptions;

                if (optionsMonitor?.CurrentValue.MinLevel <= LogLevel.Information)
                {
                    optionsMonitor.CurrentValue.MinLevel = LogLevel.Trace;
                    optionsMonitor.Set(optionsMonitor.CurrentValue);
                }
            }

            _clientVersion = request.Capabilities?.GetClientVersion() ?? ClientVersion.Lsp2;
            _serializer.SetClientCapabilities(_clientVersion.Value, request.Capabilities);

            var supportedCapabilities = new List<ISupports>();
            if (_clientVersion == ClientVersion.Lsp3)
            {
                if (request.Capabilities.TextDocument != null)
                {
                    supportedCapabilities.AddRange(
                        LspHandlerDescriptorHelpers.GetSupportedCapabilities(request.Capabilities.TextDocument)
                    );
                }

                if (request.Capabilities.Workspace != null)
                {
                    supportedCapabilities.AddRange(
                        LspHandlerDescriptorHelpers.GetSupportedCapabilities(request.Capabilities.Workspace)
                    );
                }
            }

            _supportedCapabilities.Add(supportedCapabilities);

            ClientSettings.Capabilities ??= new ClientCapabilities();
            var textDocumentCapabilities =
                ClientSettings.Capabilities.TextDocument ??= new TextDocumentClientCapabilities();
            var workspaceCapabilities = ClientSettings.Capabilities.Workspace ??= new WorkspaceClientCapabilities();
            var windowCapabilities = ClientSettings.Capabilities.Window ??= new WindowClientCapabilities();
            _serverWorkDoneManager.Initialized(windowCapabilities);

            {
                RegisterHandlers(_collection);
            }

            await Task.WhenAll(_initializeDelegates.Select(c => c(this, request, token)));

            var ccp = new ClientCapabilityProvider(_collection, windowCapabilities.WorkDoneProgress);

            var serverCapabilities = new ServerCapabilities() {
                CodeActionProvider = ccp.GetStaticOptions(textDocumentCapabilities.CodeAction)
                    .Get<ICodeActionOptions, CodeActionOptions>(CodeActionOptions.Of),
                CodeLensProvider = ccp.GetStaticOptions(textDocumentCapabilities.CodeLens)
                    .Get<ICodeLensOptions, CodeLensOptions>(CodeLensOptions.Of),
                CompletionProvider = ccp.GetStaticOptions(textDocumentCapabilities.Completion)
                    .Get<ICompletionOptions, CompletionOptions>(CompletionOptions.Of),
                DefinitionProvider = ccp.GetStaticOptions(textDocumentCapabilities.Definition)
                    .Get<IDefinitionOptions, DefinitionOptions>(DefinitionOptions.Of),
                DocumentFormattingProvider = ccp.GetStaticOptions(textDocumentCapabilities.Formatting)
                    .Get<IDocumentFormattingOptions, DocumentFormattingOptions>(DocumentFormattingOptions.Of),
                DocumentHighlightProvider = ccp.GetStaticOptions(textDocumentCapabilities.DocumentHighlight)
                    .Get<IDocumentHighlightOptions, DocumentHighlightOptions>(DocumentHighlightOptions.Of),
                DocumentLinkProvider = ccp.GetStaticOptions(textDocumentCapabilities.DocumentLink)
                    .Get<IDocumentLinkOptions, DocumentLinkOptions>(DocumentLinkOptions.Of),
                DocumentOnTypeFormattingProvider = ccp.GetStaticOptions(textDocumentCapabilities.OnTypeFormatting)
                    .Get<IDocumentOnTypeFormattingOptions, DocumentOnTypeFormattingOptions>(
                        DocumentOnTypeFormattingOptions.Of),
                DocumentRangeFormattingProvider = ccp.GetStaticOptions(textDocumentCapabilities.RangeFormatting)
                    .Get<IDocumentRangeFormattingOptions, DocumentRangeFormattingOptions>(DocumentRangeFormattingOptions
                        .Of),
                DocumentSymbolProvider = ccp.GetStaticOptions(textDocumentCapabilities.DocumentSymbol)
                    .Get<IDocumentSymbolOptions, DocumentSymbolOptions>(DocumentSymbolOptions.Of),
                ExecuteCommandProvider = ccp.GetStaticOptions(workspaceCapabilities.ExecuteCommand)
                    .Reduce<IExecuteCommandOptions, ExecuteCommandOptions>(ExecuteCommandOptions.Of),
                TextDocumentSync = ccp.GetStaticOptions(textDocumentCapabilities.Synchronization)
                    .Reduce<ITextDocumentSyncOptions, TextDocumentSyncOptions>(TextDocumentSyncOptions.Of),
                HoverProvider = ccp.GetStaticOptions(textDocumentCapabilities.Hover)
                    .Get<IHoverOptions, HoverOptions>(HoverOptions.Of),
                ReferencesProvider = ccp.GetStaticOptions(textDocumentCapabilities.References)
                    .Get<IReferencesOptions, ReferencesOptions>(ReferencesOptions.Of),
                RenameProvider = ccp.GetStaticOptions(textDocumentCapabilities.Rename)
                    .Get<IRenameOptions, RenameOptions>(RenameOptions.Of),
                SignatureHelpProvider = ccp.GetStaticOptions(textDocumentCapabilities.SignatureHelp)
                    .Get<ISignatureHelpOptions, SignatureHelpOptions>(SignatureHelpOptions.Of),
                WorkspaceSymbolProvider = ccp.GetStaticOptions(workspaceCapabilities.Symbol)
                    .Get<IWorkspaceSymbolOptions, WorkspaceSymbolOptions>(WorkspaceSymbolOptions.Of),
                ImplementationProvider = ccp.GetStaticOptions(textDocumentCapabilities.Implementation)
                    .Get<IImplementationOptions, ImplementationOptions>(ImplementationOptions.Of),
                TypeDefinitionProvider = ccp.GetStaticOptions(textDocumentCapabilities.TypeDefinition)
                    .Get<ITypeDefinitionOptions, TypeDefinitionOptions>(TypeDefinitionOptions.Of),
                ColorProvider = ccp.GetStaticOptions(textDocumentCapabilities.ColorProvider)
                    .Get<IDocumentColorOptions, DocumentColorOptions>(DocumentColorOptions.Of),
                FoldingRangeProvider = ccp.GetStaticOptions(textDocumentCapabilities.FoldingRange)
                    .Get<IFoldingRangeOptions, FoldingRangeOptions>(FoldingRangeOptions.Of),
                SelectionRangeProvider = ccp.GetStaticOptions(textDocumentCapabilities.FoldingRange)
                    .Get<ISelectionRangeOptions, SelectionRangeOptions>(SelectionRangeOptions.Of),
                DeclarationProvider = ccp.GetStaticOptions(textDocumentCapabilities.Declaration)
                    .Get<IDeclarationOptions, DeclarationOptions>(DeclarationOptions.Of),
#pragma warning disable 618
                CallHierarchyProvider = ccp.GetStaticOptions(textDocumentCapabilities.CallHierarchy)
                    .Get<ICallHierarchyOptions, CallHierarchyOptions>(CallHierarchyOptions.Of),
                SemanticTokensProvider = ccp.GetStaticOptions(textDocumentCapabilities.SemanticTokens)
                    .Get<ISemanticTokensOptions, SemanticTokensOptions>(SemanticTokensOptions.Of),
#pragma warning restore 618
            };

            if (_collection.ContainsHandler(typeof(IDidChangeWorkspaceFoldersHandler)))
            {
                serverCapabilities.Workspace = new WorkspaceServerCapabilities() {
                    WorkspaceFolders = new WorkspaceFolderOptions() {
                        Supported = true,
                        ChangeNotifications = Guid.NewGuid().ToString()
                    }
                };
            }

            if (ccp.HasStaticHandler(textDocumentCapabilities.Synchronization))
            {
                var textDocumentSyncKind = TextDocumentSyncKind.None;
                if (_collection.ContainsHandler(typeof(IDidChangeTextDocumentHandler)))
                {
                    var kinds = _collection
                        .Select(x => x.Handler)
                        .OfType<IDidChangeTextDocumentHandler>()
                        .Select(x => x.GetRegistrationOptions()?.SyncKind ?? TextDocumentSyncKind.None)
                        .Where(x => x != TextDocumentSyncKind.None)
                        .ToArray();
                    if (kinds.Any())
                    {
                        textDocumentSyncKind = kinds.Min(z => z);
                    }
                }

                if (_clientVersion == ClientVersion.Lsp2)
                {
                    serverCapabilities.TextDocumentSync = textDocumentSyncKind;
                }
                else
                {
                    serverCapabilities.TextDocumentSync = new TextDocumentSyncOptions() {
                        Change = textDocumentSyncKind,
                        OpenClose = _collection.ContainsHandler(typeof(IDidOpenTextDocumentHandler)) ||
                                    _collection.ContainsHandler(typeof(IDidCloseTextDocumentHandler)),
                        Save = _collection.ContainsHandler(typeof(IDidSaveTextDocumentHandler))
                            ? new SaveOptions() {IncludeText = true /* TODO: Make configurable */}
                            : null,
                        WillSave = _collection.ContainsHandler(typeof(IWillSaveTextDocumentHandler)),
                        WillSaveWaitUntil = _collection.ContainsHandler(typeof(IWillSaveWaitUntilTextDocumentHandler))
                    };
                }
            }

            // TODO: Need a call back here
            // serverCapabilities.Experimental;

            _serverReceiver.Initialized();

            var result = ServerSettings = new InitializeResult() {
                Capabilities = serverCapabilities,
                ServerInfo = _serverInfo
            };

            await Task.WhenAll(_initializedDelegates.Select(c => c(this, request, result, token)));

            foreach (var item in _collection)
            {
                LspHandlerDescriptorHelpers.InitializeHandler(item, _supportedCapabilities, item.Handler);
            }

            // TODO:
            if (_clientVersion == ClientVersion.Lsp2)
            {
                _initializeComplete.OnNext(result);
                _initializeComplete.OnCompleted();
            }

            return result;
        }

        public async Task<MediatR.Unit> Handle(InitializedParams @params, CancellationToken token)
        {
            if (_clientVersion == ClientVersion.Lsp3)
            {
                _initializeComplete.OnNext(ServerSettings);
                _initializeComplete.OnCompleted();
            }

            return MediatR.Unit.Value;
        }

        private async Task DynamicallyRegisterHandlers(Registration[] registrations)
        {
            if (registrations.Length == 0)
                return; // No dynamic registrations supported by client.

            var @params = new RegistrationParams() {Registrations = registrations};

            await _initializeComplete;

            await Client.RegisterCapability(@params);
        }

        public IObservable<InitializeResult> Start => _initializeComplete.AsObservable();

        public Task<InitializeResult> WasStarted => _initializeComplete.ToTask();

        public void Dispose()
        {
            _disposable?.Dispose();
            _connection?.Dispose();
        }

        public IDictionary<string, JToken> Experimental { get; } = new Dictionary<string, JToken>();

        protected override IResponseRouter ResponseRouter => _responseRouter;
        protected override IHandlersManager HandlersManager => _collection;

        public IDisposable Register(Action<ILanguageServerRegistry> registryAction)
        {
            var manager = new CompositeHandlersManager(_collection);
            registryAction(new LangaugeServerRegistry(_fallbackServiceProvider, manager, _textDocumentIdentifiers));
            return RegisterHandlers(manager.GetDisposable());
        }

        private IDisposable RegisterHandlers(IEnumerable<ILspHandlerDescriptor> collection)
        {
            var registrations = new List<Registration>();
            foreach (var descriptor in collection)
            {
                if (descriptor is LspHandlerDescriptor lspHandlerDescriptor &&
                    lspHandlerDescriptor.TypeDescriptor?.HandlerType != null &&
                    typeof(IDoesNotParticipateInRegistration).IsAssignableFrom(lspHandlerDescriptor.TypeDescriptor.HandlerType))
                {
                    continue;
                }

                if (descriptor.HasCapability && _supportedCapabilities.AllowsDynamicRegistration(descriptor.CapabilityType))
                {
                    if (descriptor.RegistrationOptions is IWorkDoneProgressOptions wdpo)
                    {
                        wdpo.WorkDoneProgress = _serverWorkDoneManager.IsSupported;
                    }

                    registrations.Add(new Registration() {
                        Id = descriptor.Id.ToString(),
                        Method = descriptor.Method,
                        RegisterOptions = descriptor.RegistrationOptions
                    });
                }

                if (descriptor.OnServerStartedDelegate != null)
                {
                    // Fire and forget to initialize the handler
                    _initializeComplete
                        .Select(result =>
                            Observable.FromAsync((ct) => descriptor.OnServerStartedDelegate(this, result, ct)))
                        .Merge()
                        .Subscribe();
                }
            }

            // Fire and forget
            DynamicallyRegisterHandlers(registrations.ToArray()).ToObservable().Subscribe();

            return Disposable.Create(() => {
                Client.UnregisterCapability(new UnregistrationParams() {
                    Unregisterations = registrations.ToArray()
                }).ToObservable().Subscribe();
            });
        }

        private IDisposable RegisterHandlers(IDisposable handlerDisposable)
        {
            if (handlerDisposable is LspHandlerDescriptorDisposable lsp)
            {
                return new CompositeDisposable(lsp, RegisterHandlers(lsp.Descriptors));
            }

            if (!(handlerDisposable is CompositeDisposable cd)) return Disposable.Empty;
            cd.Add(RegisterHandlers(cd.OfType<LspHandlerDescriptorDisposable>().SelectMany(z => z.Descriptors)));
            return cd;
        }
    }
}
