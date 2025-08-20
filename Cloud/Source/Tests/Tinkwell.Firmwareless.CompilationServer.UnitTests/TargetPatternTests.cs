
using FluentAssertions;
using Tinkwell.Firmwareless.CompilationServer.Services;
using Xunit;

namespace Tinkwell.Firmwareless.CompilationServer.UnitTests;

public class TargetPatternTests
{
    [Theory]
    [InlineData("x86_64-*-linux-gnu", "x86_64-pc-linux-gnu", true)]
    [InlineData("x86_64-*-linux-gnu", "x86_64-unknown-linux-gnu", true)]
    [InlineData("x86_64-*-linux-gnu", "aarch64-pc-linux-gnu", false)]
    [InlineData("armv7|arm-*-linux-gnueabihf", "armv7", true)]
    [InlineData("armv7|arm-*-linux-gnueabihf", "arm-unknown-linux-gnueabihf", true)]
    [InlineData("armv7|arm-*-linux-gnueabihf", "armv7-unknown-linux-gnueabihf", false)]
    [InlineData("armv7|arm-*-linux-gnueabihf", "armv8-unknown-linux-gnueabihf", false)]
    [InlineData("*", "anything", true)]
    [InlineData("*", "any-thing", false)]
    [InlineData("*-*", "any-thing", true)]
    [InlineData("*-*", "anything", false)]
    public void IsMatch_ShouldReturnExpectedResult(string pattern, string text, bool expected)
    {
        // Act
        var result = TargetPattern.IsMatch(pattern, text);

        // Assert
        result.Should().Be(expected);
    }
}
