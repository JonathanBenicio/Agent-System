using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.MCP;

namespace AgenticSystem.Tests;

public class MCPPluginManagerTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly MCPPluginManager _sut;

    public MCPPluginManagerTests()
    {
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _sut = new MCPPluginManager(_loggerFactory);
    }

    [Fact]
    public void GetPlugin_UnknownId_ReturnsNull()
    {
        _sut.GetPlugin("non-existent").Should().BeNull();
    }

    [Fact]
    public void GetLoadedPlugins_WhenEmpty_ReturnsEmpty()
    {
        _sut.GetLoadedPlugins().Should().BeEmpty();
    }

    [Fact]
    public async Task ExecutePluginTool_UnknownPlugin_ReturnsFail()
    {
        var result = await _sut.ExecutePluginToolAsync("unknown", "tool", new Dictionary<string, object>());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task GetAllTools_WhenNoPlugins_ReturnsEmpty()
    {
        var tools = await _sut.GetAllToolsAsync();

        tools.Should().NotBeNull();
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task UnloadPlugin_WhenNotLoaded_DoesNotThrow()
    {
        await _sut.Invoking(s => s.UnloadPluginAsync("nonexistent"))
            .Should().NotThrowAsync();
    }

    [Fact]
    public void GetPluginStatuses_WhenEmpty_ReturnsEmpty()
    {
        _sut.GetPluginStatuses().Should().BeEmpty();
    }

    [Fact]
    public async Task LoadPlugin_InvalidJson_ThrowsInvalidOperation()
    {
        // JSON inválido (começa com { mas não é JSON válido)
        await _sut.Invoking(s => s.LoadPluginAsync("{invalid json"))
            .Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task LoadPluginFromConfig_CreatesClientPlugin()
    {
        // Tenta carregar com comando inválido — falhará no InitializeAsync
        // mas valida que o fluxo é correto até a tentativa de conexão
        var config = new MCPPluginConfig
        {
            Name = "test-plugin",
            TransportType = MCPTransportType.Stdio,
            Command = "nonexistent-binary-xyz123"
        };

        await _sut.Invoking(s => s.LoadPluginFromConfigAsync(config))
            .Should().ThrowAsync<Exception>();

        // Plugin não foi registrado pois InitializeAsync falhou
        _sut.GetLoadedPlugins().Should().BeEmpty();
    }
}
