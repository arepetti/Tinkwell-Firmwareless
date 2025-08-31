using System.Runtime.InteropServices;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

static class HostExportedFunctions
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void NativeAbortDelegate(nint execEnv, nint messagePtr, nint fileNamePtr, int line, int column);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NativeMqttPublishDelegate(nint execEnv, nint topicPtr, int topicLen, nint payloadPtr, int payloadLen);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NativeLogDelegate(nint execEnv, int severity, nint topicPtr, int topicLen, nint payloadPtr, int payloadLen);

    public static void Abort(nint execEnv, nint messagePtr, nint fileNamePtr, int line, int column)
    {
        Console.WriteLine($"Fatal error!");
        Environment.Exit(1);
    }

    public static int MqttPublish(nint execEnv, nint topicPtr, int topicLen, nint payloadPtr, int payloadLen)
    {
        try
        {
            nint moduleInstance = WamrHost.GetModuleInstanceFromExecEnvHandle(execEnv);
            string topic = WasmMemory.PtrToStringUtf8(moduleInstance, topicPtr, topicLen);
            string payload = WasmMemory.PtrToStringUtf8(moduleInstance, payloadPtr, payloadLen);

            Console.WriteLine($"[HOST] MQTT PUBLISH topic='{topic}' payload='{payload}'");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[HOST] tw_mqtt_publish() error: {ex.Message}");
            return -2;
        }
    }

    public static int Log(nint execEnv, int severity, nint topicPtr, int topicLen, nint messagePtr, int messageLen)
    {
        try
        {
            nint moduleInstance = WamrHost.GetModuleInstanceFromExecEnvHandle(execEnv);
            string topic = WasmMemory.PtrToStringUtf8(moduleInstance, topicPtr, topicLen);
            string message = WasmMemory.PtrToStringUtf8(moduleInstance, messagePtr, messageLen);

            Console.WriteLine($"{severity} - {topic}: {message}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[HOST] tw_log() error: {ex.Message}");
            return -2;
        }
    }
}
