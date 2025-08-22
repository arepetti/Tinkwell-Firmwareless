using FluentAssertions;
using System.Collections.Generic;
using System.IO;
using Tinkwell.Firmwareless;
using Tinkwell.Firmwareless.CompilationServer.Services;
using Xunit;

namespace Tinkwell.Firmwareless.CompilationServer.UnitTests;

public class CompilerOptionsBuilderTests
{
    private const string TestMetaArchFile = "test-meta-architectures.yml";
    private const string TestValidationFile = "test-target-validation.yml";
    private const string TestOptionsFile = "test-target-options.yml";

    public CompilerOptionsBuilderTests()
    {
        // Create dummy YAML files for testing
        File.WriteAllText(TestMetaArchFile, MetaArchYaml);
        File.WriteAllText(TestValidationFile, ValidationYaml);
        File.WriteAllText(TestOptionsFile, OptionsYaml);
    }

    [Fact]
    public void UseMetaArchitectures_WithValidMetaName_ShouldResolveCorrectly()
    {
        // Arrange
        var target = CompilationTarget.Parse("test_linux");
        var builder = new CompilerOptionsBuilder(target);

        // Act
        builder.UseMetaArchitectures(TestMetaArchFile);
        builder.UseValidation(TestValidationFile);
        builder.UseCompilerConfiguration(TestOptionsFile);
        var args = builder.Build();

        // Assert
        string.Join(' ', args).Should().Contain("--target=x86_64-pc-linux-gnu");
    }

    [Fact]
    public void UseValidation_WithInvalidCombination_ShouldThrowArgumentException()
    {
        // Arrange
        var target = CompilationTarget.Parse("x86_64-apple-linux-gnu"); // Invalid: apple vendor on linux
        var builder = new CompilerOptionsBuilder(target);
        builder.UseMetaArchitectures(TestMetaArchFile);

        // Act
        var action = () => builder.UseValidation(TestValidationFile);

        // Assert
        action.Should().Throw<ArgumentException>().WithMessage("*is not a valid or supported combination*");
    }

    [Fact]
    public void UseCompilerConfiguration_WithFeatures_ShouldAddFlags()
    {
        // Arrange
        var target = CompilationTarget.Parse("test_linux~crypto");
        var builder = new CompilerOptionsBuilder(target);

        // Act
        builder.UseMetaArchitectures(TestMetaArchFile);
        builder.UseValidation(TestValidationFile);
        builder.UseCompilerConfiguration(TestOptionsFile);
        var args = builder.Build();

        // Assert
        string.Join(' ', args).Should().Contain("--target=x86_64-pc-linux-gnu");
        string.Join(' ', args).Should().Contain("-mattr=+aes");
    }

    [Fact]
    public void Build_WithOptions_ShouldAddCorrectFlags()
    {
        // Arrange
        var target = CompilationTarget.Parse("test_linux");
        var builder = new CompilerOptionsBuilder(target);
        builder.UseMetaArchitectures(TestMetaArchFile);
        builder.UseValidation(TestValidationFile);
        builder.UseCompilerConfiguration(TestOptionsFile);
        builder.WithFiles([("input.wasm", "output.aot")]);

        var options = new CompilerOptionsBuilderOptions
        {
            EnableGarbageCollection = true,
            EnableMultiThread = true,
            VerboseLog = true
        };

        // Act
        var args = builder.Build(options);

        // Assert
        string.Join(' ', args).Should().Contain("--enable-gc");
        string.Join(' ', args).Should().Contain("--enable-multi-thread");
        string.Join(' ', args).Should().Contain("-v=5");
        string.Join(' ', args).Should().Contain("-o \"output.aot\" \"input.wasm\"");
    }

    private const string MetaArchYaml = @"
schema: 1
normalize:
  architecture:
    x64: x86_64
  os:
    win: windows
meta:
  test_linux:
    set:
      architecture: x86_64
      vendor: pc
      os: linux
      abi: gnu
  test_linux_crypto:
    set:
      architecture: x86_64
      vendor: pc
      os: linux
      abi: gnu
    features: [crypto]
";

    private const string ValidationYaml = @"
schema: 1
validate:
  components:
    architecture: { any: [x86_64, aarch64] }
    vendor: { any: [pc, apple] }
    os: { any: [linux, windows, darwin] }
    abi: { any: [gnu, msvc, none] }
  rules:
    - when: { os: linux }
      require: { vendor: pc }
    - when: { os: darwin }
      require: { vendor: apple }
    - when: { os: windows }
      require: { vendor: pc }
";

    private const string OptionsYaml = @"
schema: 1
flags:
  x86_64-pc-linux-gnu: [""--target=x86_64-pc-linux-gnu""]
  x86_64-pc-windows-msvc: [""--target=x86_64-pc-windows-msvc""]
features:
  x86_64:
    crypto: [""-mattr=+aes""]
";
}
