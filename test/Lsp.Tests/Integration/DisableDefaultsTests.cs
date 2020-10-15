﻿using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NSubstitute.Exceptions;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Testing;
using OmniSharp.Extensions.LanguageProtocol.Testing;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.Extensions.LanguageServer.Shared;
using TestingUtils;
using Xunit;
using Xunit.Abstractions;

namespace Lsp.Tests.Integration
{
    public class DisableDefaultsTests : LanguageProtocolTestBase
    {
        public DisableDefaultsTests(ITestOutputHelper outputHelper) : base(new JsonRpcTestOptions().ConfigureForXUnit(outputHelper))
        {
        }

        [Fact]
        public async Task Should_Disable_Registration_Manager()
        {
            var registrationAction = Substitute.For<Func<RegistrationParams, Task>>();
            var unregistrationAction = Substitute.For<Func<UnregistrationParams, Task>>();
            var (client, _) = await Initialize(
                options => options
                   .OnRegisterCapability(registrationAction)
                          .OnUnregisterCapability(unregistrationAction),
                options => { }
            );

            var clientManager = client.Services.GetRequiredService<IHandlersManager>();
            clientManager.Descriptors.Should().Contain(f => f.Handler is DelegatingHandlers.Request<RegistrationParams>);
            clientManager.Descriptors.Should().Contain(f => f.Handler is DelegatingHandlers.Request<UnregistrationParams>);
        }

        [Fact]
        public async Task Should_Disable_Workspace_Folder_Manager()
        {
            var clientAction = Substitute.For<Func<WorkspaceFolderParams, Task<Container<WorkspaceFolder>?>>>();
            var serverAction = Substitute.For<Action<DidChangeWorkspaceFoldersParams>>();
            var (client, server) = await Initialize(
                options => options.OnWorkspaceFolders(clientAction),
                options => options.OnDidChangeWorkspaceFolders(serverAction, new object())
            );

            var clientManager = client.Services.GetRequiredService<IHandlersManager>();
            clientManager.Descriptors.Should().Contain(f => f.Handler is DelegatingHandlers.Request<WorkspaceFolderParams, Container<WorkspaceFolder>?>);

            var serverManager = server.Services.GetRequiredService<IHandlersManager>();
            serverManager.Descriptors.Should().Contain(f => f.Handler is LanguageProtocolDelegatingHandlers.Notification<DidChangeWorkspaceFoldersParams, object>);
        }

        [Fact]
        public async Task Should_Allow_Custom_Workspace_Folder_Manager_Delegate()
        {
            var action = Substitute.For<Action<DidChangeWorkspaceFoldersParams>>();
            var (client, server) = await Initialize(
                options => {},
                options => options
                          .OnDidChangeWorkspaceFolders(action, new object())
            );

            var config = client.Services.GetRequiredService<TestConfigurationProvider>();
            config.Update("mysection", new Dictionary<string, string>() { ["data"] = "value" });

            client.WorkspaceFoldersManager.Add(new WorkspaceFolder() { Name = "foldera", Uri = "/some/path" });

            await TestHelper.DelayUntil(
                () => {
                    try
                    {
                        action.Received(1).Invoke(Arg.Any<DidChangeWorkspaceFoldersParams>());
                        return true;
                    }
                    catch (ReceivedCallsException e)
                    {
                        return false;
                    }
                }, CancellationToken
            );
        }

        [Fact]
        public async Task Should_Disable_Configuration()
        {
            var action = Substitute.For<Action<DidChangeConfigurationParams>>();
            var (_, server) = await Initialize(
                options => { },
                options => options.OnDidChangeConfiguration(action, new object())
            );

            var serverManager = server.Services.GetRequiredService<IHandlersManager>();
            serverManager.Descriptors.Should().Contain(f => f.Handler is LanguageProtocolDelegatingHandlers.Notification<DidChangeConfigurationParams, object>);
        }

        [Fact]
        public async Task Should_Allow_Custom_Configuration_Delegate()
        {
            var action = Substitute.For<Action<DidChangeConfigurationParams>>();
            var (client, server) = await Initialize(
                options => options
                          .WithCapability(new DidChangeConfigurationCapability() { DynamicRegistration = true })
                          .WithServices(z => z.AddSingleton<TestConfigurationProvider>()),
                options => options
                          .WithConfigurationSection("mysection")
                          .OnDidChangeConfiguration(action, new object())
            );

            var clientManager = client.Services.GetRequiredService<SharedHandlerCollection>();
            clientManager.ContainsHandler(typeof(IConfigurationHandler)).Should().BeTrue();

            var serverManager = server.Services.GetRequiredService<SharedHandlerCollection>();
            serverManager.ContainsHandler(typeof(IDidChangeConfigurationHandler)).Should().BeTrue();

            var config = client.Services.GetRequiredService<TestConfigurationProvider>();
            config.Update("mysection", new Dictionary<string, string>() { ["data"] = "value" });

            await TestHelper.DelayUntil(
                () => {
                    try
                    {
                        action.Received(1).Invoke(Arg.Is<DidChangeConfigurationParams>(z => Equals(z.Settings, JValue.CreateNull())));
                        return true;
                    }
                    catch (ReceivedCallsException e)
                    {
                        return false;
                    }
                }, CancellationToken
            );
        }
    }
}
