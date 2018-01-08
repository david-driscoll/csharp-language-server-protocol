using System;
using FluentAssertions;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using Xunit;

namespace Lsp.Tests.Models
{
    public class PublishDiagnosticsParamsTests
    {
        [Theory, JsonFixture]
        public void SimpleTest(string expected)
        {
            var model = new PublishDiagnosticsParams() {
                Uri = new Uri("file:///abc/123.cs"),
                Diagnostics = new[] {
                    new Diagnostic() {
                        Code = new DiagnosticCode("abcd"),
                        Message = "message",
                        Range = new Range(new Position(1, 1), new Position(2, 2)),
                        Severity = DiagnosticSeverity.Error,
                        Source = "csharp"
                    }
                }
            };
            var result = Fixture.SerializeObject(model);

            result.Should().Be(expected);

            var deresult = new Serializer(ClientVersion.Lsp3).DeserializeObject<PublishDiagnosticsParams>(expected);
            deresult.ShouldBeEquivalentTo(model);
        }
    }
}
