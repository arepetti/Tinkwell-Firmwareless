using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

sealed class HostExportedUnsafeNativeFunctions(ILogger<HostExportedUnsafeNativeFunctions> logger, IHostExportedFunctions hostExportedFunctions)
    : IRegisterHostUnsafeNativeFunctions
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void NativeAbortDelegate(nint execEnv, nint messagePtr, nint fileNamePtr, int line, int column);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NativeMqttPublishDelegate(nint execEnv, nint topicPtr, int topicLen, nint payloadPtr, int payloadLen);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NativeLogDelegate(nint execEnv, int severity, nint topicPtr, int topicLen, nint payloadPtr, int payloadLen);

    [DynamicDependency(nameof(abort))]
    [DynamicDependency(nameof(tw_log))]
    [DynamicDependency(nameof(tw_mqtt_publish))]
    public void RegisterAll()
    {
        if (_instance is not null)
            throw new HostException($"{nameof(HostExportedUnsafeNativeFunctions)} is not used as singleton or {nameof(RegisterAll)}() has been called twice.");

        _instance = this;

        WamrHost.RegisterNativeFunctions([
            WamrHost.MakeNativeSymbol(nameof(abort),
                new NativeAbortDelegate(abort),
                WamrHost.Signature(typeof(void), typeof(nint), typeof(nint), typeof(int), typeof(int))),

            WamrHost.MakeNativeSymbol(nameof(tw_log),
                new NativeLogDelegate(tw_log),
                WamrHost.Signature(typeof(int), typeof(int), typeof(nint), typeof(int), typeof(nint), typeof(int))),

            WamrHost.MakeNativeSymbol(nameof(tw_mqtt_publish),
                new NativeMqttPublishDelegate(tw_mqtt_publish),
                WamrHost.Signature(typeof(int), typeof(nint), typeof(int), typeof(nint), typeof(int)))
        ]);
    }

    private static HostExportedUnsafeNativeFunctions? _instance;
    private readonly ILogger<HostExportedUnsafeNativeFunctions> _logger = logger;
    private readonly IHostExportedFunctions _hostExportedFunctions = hostExportedFunctions;

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Match exported name")]
    private static void abort(nint execEnv, nint messagePtr, nint fileNamePtr, int line, int column)
    {
        Debug.Assert(_instance is not null);
        try
        {
            nint moduleInstance = WamrHost.GetModuleInstanceFromExecEnvHandle(execEnv);
            string message = WasmMemory.HighlyUnsafeUtf8PtrToString(moduleInstance, messagePtr);
            string fileName = WasmMemory.HighlyUnsafeUtf8PtrToString(fileNamePtr, messagePtr);

            _instance._hostExportedFunctions.Abort(message, fileName, line, column);
        }
        catch (Exception e)
        {
            _instance._logger.LogCritical(e, "Fatal error when calling {FunctionName}(): {Message}", nameof(abort), e.Message);
            Environment.Exit(1);
        }
    }

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Match exported name")]
    private static int tw_log(nint execEnv, int severity, nint topicPtr, int topicLen, nint messagePtr, int messageLen)
    {
        Debug.Assert(_instance is not null);
        try
        {
            nint moduleInstance = WamrHost.GetModuleInstanceFromExecEnvHandle(execEnv);
            string topic = WasmMemory.Utf8PtrToString(moduleInstance, topicPtr, topicLen);
            string message = WasmMemory.Utf8PtrToString(moduleInstance, messagePtr, messageLen);

            _instance._hostExportedFunctions.Log(severity, topic, message);
            return 0;
        }
        catch (Exception e)
        {
            _instance._logger.LogError(e, "Error when calling {FunctionName}(): {Message}", nameof(tw_log), e.Message);
            return -1;
        }
    }

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Match exported name")]
    private static int tw_mqtt_publish(nint execEnv, nint topicPtr, int topicLen, nint payloadPtr, int payloadLen)
    {
        Debug.Assert(_instance is not null);
        try
        {
            nint moduleInstance = WamrHost.GetModuleInstanceFromExecEnvHandle(execEnv);
            string topic = WasmMemory.Utf8PtrToString(moduleInstance, topicPtr, topicLen);
            string payload = WasmMemory.Utf8PtrToString(moduleInstance, payloadPtr, payloadLen);

            _instance._hostExportedFunctions.PublishMqttMessage(topic, payload);
            return 0;
        }
        catch (Exception e)
        {
            _instance._logger.LogError(e, "Error when calling {FunctionName}(): {Message}", nameof(tw_mqtt_publish), e.Message);
            return -1;
        }
    }
}
