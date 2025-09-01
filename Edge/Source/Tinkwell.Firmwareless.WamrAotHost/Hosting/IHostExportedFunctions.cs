namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

interface IHostExportedFunctions
{
    void Abort(string message, string fileName, int lineNumber, int columnNumber);
    void Log(int severity, string topic, string message);
    void PublishMqttMessage(string topic, string payload);
}
