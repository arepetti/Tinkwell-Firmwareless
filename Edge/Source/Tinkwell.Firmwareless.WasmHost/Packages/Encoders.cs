using System.Text;

namespace Tinkwell.Firmwareless.WasmHost.Packages;

static class Encoders
{
    public readonly static Encoding UTF8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}
