namespace Tinkwell.Firmwareless.WasmHost.Packages;

class InvalidFirmwarePackageException : TinkwellException
{
    public InvalidFirmwarePackageException(string message)
        : base(message)
    {
    }
    public InvalidFirmwarePackageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

sealed class FirmwareValidationException(string message) : InvalidFirmwarePackageException(message);

sealed class FirmwareCompatibilityException(string message) : InvalidFirmwarePackageException(message);
