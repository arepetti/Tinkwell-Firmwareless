namespace Tinkwell.Firmwareless.WasmHost;

class WasmHostException : TinkwellException
{
    public WasmHostException(string message) : base(message) { }
    public WasmHostException(string message, Exception inner) : base(message, inner) { }
}
