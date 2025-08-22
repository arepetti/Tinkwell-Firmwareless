
using FluentAssertions;
using Tinkwell.Firmwareless;
using Xunit;

namespace Tinkwell.Firmwareless.UnitTests;

public class CompilationTargetTests
{
    [Theory]
    [InlineData("x86_64-pc-linux-gnu", "x86_64", "pc", "linux", "gnu", new string[0])]
    [InlineData("armv7-unknown-linux-gnueabihf", "armv7", "unknown", "linux", "gnueabihf", new string[0])]
    [InlineData("aarch64-apple-darwin-none", "aarch64", "apple", "darwin", "none", new string[0])]
    [InlineData("riscv64-unknown-linux-gnu~feature1,feature2", "riscv64", "unknown", "linux", "gnu", new[] { "feature1", "feature2" })]
    [InlineData("x86_64-pc-windows-msvc", "x86_64", "pc", "windows", "msvc", new string[0])]
    [InlineData("armv7", "armv7", "", "", "", new string[0])]
    [InlineData("armv7~feature1", "armv7", "", "", "", new[] { "feature1" })]
    public void Parse_ValidTargetString_ReturnsCorrectCompilationTarget(
        string input, string expectedArchitecture, string expectedVendor, string expectedOs, string expectedAbi, string[] expectedFeatures)
    {
        // Act
        var target = CompilationTarget.Parse(input);

        // Assert
        target.Architecture.Should().Be(expectedArchitecture);
        target.Vendor.Should().Be(expectedVendor);
        target.Os.Should().Be(expectedOs);
        target.Abi.Should().Be(expectedAbi);
        target.Features.Should().BeEquivalentTo(expectedFeatures);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-format")]
    [InlineData("too-many-parts-a-b-c-d-e")]
    [InlineData("x86_64-pc-linux-gnu~feature,invalid-feature!")]
    public void Parse_InvalidTargetString_ThrowsFormatException(string input)
    {
        // Act
        Action act = () => CompilationTarget.Parse(input);

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ToString_ReturnsCorrectFormat()
    {
        // Arrange
        var target = new CompilationTarget("x86_64", "pc", "linux", "gnu", ["feature1", "feature2"]);

        // Act
        var result = target.ToString();

        // Assert
        result.Should().Be("x86_64-pc-linux-gnu~feature1,feature2");
    }

    [Fact]
    public void ToString_WithoutFeatures_ReturnsCorrectFormat()
    {
        // Arrange
        var target = new CompilationTarget("armv7", "unknown", "unknown", "unknown", []);

        // Act
        var result = target.ToString();

        // Assert
        result.Should().Be("armv7-unknown-unknown-unknown");
    }
}
