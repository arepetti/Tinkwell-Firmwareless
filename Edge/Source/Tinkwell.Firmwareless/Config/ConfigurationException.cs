namespace Tinkwell.Firmwareless.Config;

public sealed class ConfigurationException : TinkwellException
{
    public ConfigurationException(string message) : base(message) { }
    public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}
