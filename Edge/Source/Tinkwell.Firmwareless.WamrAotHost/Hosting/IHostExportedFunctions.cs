using Tinkwell.Firmwareless.Vfs;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

interface IHostExportedFunctions
{
    void Abort(string message, string fileName, int lineNumber, int columnNumber);
    void Log(int severity, string topic, string message);
    void PublishMqttMessage(string topic, string payload);
    int OpenFile(string path, OpenMode mode, OpenFlags flags);
    void CloseFile(int handle);
    int ReadFromFile(int handle, Span<byte> buffer, ReadFlags flags);
    int WriteToFile(int handle, Span<byte> buffer, WriteFlags flags);
}
