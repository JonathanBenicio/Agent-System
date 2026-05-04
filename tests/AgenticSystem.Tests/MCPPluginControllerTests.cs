using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Api.Controllers;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Tests;

public class MCPPluginControllerTests
{
    private readonly IMCPPluginManager _pluginManager;
    private readonly MCPPluginController _sut;

    public MCPPluginControllerTests()
    {
        _pluginManager = Substitute.For<IMCPPluginManager>();
        var logger = Substitute.For<ILogger<MCPPluginController>>();
        _sut = new MCPPluginController(_pluginManager, logger);
    }

    [Fact]
    public void GetPlugins_ReturnsOkWithPluginList()
    {
        var plugin = CreateMockPlugin("p1", "Plugin 1");
        _pluginManager.GetLoadedPlugins().Returns(new[] { plugin });

        var result = _sut.GetPlugins();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetPlugin_WhenExists_ReturnsOk()
    {
        var plugin = CreateMockPlugin("p1", "Plugin 1");
        _pluginManager.GetPlugin("p1").Returns(plugin);

        var result = _sut.GetPlugin("p1");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetPlugin_WhenNotExists_ReturnsNotFound()
    {
        _pluginManager.GetPlugin("nonexistent").Returns((IMCPPlugin?)null);

        var result = _sut.GetPlugin("nonexistent");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task LoadPlugin_WithValidConfig_ReturnsCreated()
    {
        var plugin = CreateMockPlugin("p1", "Plugin 1");
        _pluginManager.LoadPluginAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(plugin);

        var config = new MCPPluginConfig { Name = "Plugin 1", Command = "npx server" };
        var result = await _sut.LoadPlugin(config, CancellationToken.None);

        // Falls through to interface fallback since mock is not MCPPluginManager
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task LoadPlugin_WithEmptyCommandAndEndpoint_ReturnsBadRequest()
    {
        var config = new MCPPluginConfig { Name = "bad", Command = "", Endpoint = "" };
        var result = await _sut.LoadPlugin(config, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UnloadPlugin_WhenExists_ReturnsNoContent()
    {
        var plugin = CreateMockPlugin("p1", "Plugin 1");
        _pluginManager.GetPlugin("p1").Returns(plugin);

        var result = await _sut.UnloadPlugin("p1", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UnloadPlugin_WhenNotExists_ReturnsNotFound()
    {
        _pluginManager.GetPlugin("nonexistent").Returns((IMCPPlugin?)null);

        var result = await _sut.UnloadPlugin("nonexistent", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetAllTools_ReturnsOk()
    {
        _pluginManager.GetAllToolsAsync().Returns(new List<MCPToolInfo>());

        var result = await _sut.GetAllTools();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ExecutePluginTool_WhenSuccess_ReturnsOk()
    {
        _pluginManager.ExecutePluginToolAsync("p1", "tool", Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(MCPResponse.Ok("result"));

        var result = await _sut.ExecutePluginTool("p1", "tool", new Dictionary<string, object>(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ExecutePluginTool_WhenFail_ReturnsBadRequest()
    {
        _pluginManager.ExecutePluginToolAsync("p1", "tool", Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(MCPResponse.Fail("error"));

        var result = await _sut.ExecutePluginTool("p1", "tool", new Dictionary<string, object>(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void GetPluginResources_WhenNotFound_ReturnsNotFound()
    {
        _pluginManager.GetPlugin("nonexistent").Returns((IMCPPlugin?)null);

        var result = _sut.GetPluginResources("nonexistent");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void GetStatus_ReturnsOk()
    {
        // NSubstitute returns empty enumerable by default for GetLoadedPlugins()
        var result = _sut.GetStatus();

        result.Should().BeOfType<OkObjectResult>();
    }

    private IMCPPlugin CreateMockPlugin(string id, string name)
    {
        var plugin = Substitute.For<IMCPPlugin>();
        plugin.Id.Returns(id);
        plugin.Name.Returns(name);
        plugin.Description.Returns($"Description for {name}");
        plugin.Version.Returns("1.0.0");
        plugin.IsEnabled.Returns(true);
        plugin.ProvidedTools.Returns(new List<string> { "tool1" });
        plugin.ProvidedResources.Returns(new List<string>());
        return plugin;
    }
}
