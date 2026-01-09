namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

enum WasmErrorCode
{
    Ok = 0,
    Generic = -1,
    Host = -2,
    OutOfMemory = -3,
    Io = -4,
    NotSupported = -10,
    NotImplemented = -11,
    InvalidArgument = -20,
    ArgumentOutOfRange = -21,
    ArgumentInvalidFormat = -22,
    NotFound = -31,
    NoAccess = -32
}