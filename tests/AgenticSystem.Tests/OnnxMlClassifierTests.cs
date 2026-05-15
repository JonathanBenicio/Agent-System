using AgenticSystem.Core.Services.Ml;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class OnnxMlClassifierTests
{
    private readonly ILogger<OnnxMlClassifier> _logger;

    public OnnxMlClassifierTests()
    {
        _logger = Substitute.For<ILogger<OnnxMlClassifier>>();
    }

    [Fact]
    public async Task ClassifyAsync_WithRealModel_DoesNotCrash()
    {
        // Arrange
        var potentialPaths = new[] 
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fastpath_model.onnx"),
            "fastpath_model.onnx",
            Path.Combine(Directory.GetCurrentDirectory(), "fastpath_model.onnx")
        };

        var modelPath = potentialPaths.FirstOrDefault(File.Exists);
        
        if (modelPath == null)
        {
            // Skip test if model not found
            return;
        }
        
        using var sut = new OnnxMlClassifier(modelPath, _logger);

        // Act
        var result = await sut.ClassifyAsync("teste de classificação");

        // Assert
        result.Should().NotBeNull();
        result.Label.Should().NotBeNullOrWhiteSpace();
    }
}
