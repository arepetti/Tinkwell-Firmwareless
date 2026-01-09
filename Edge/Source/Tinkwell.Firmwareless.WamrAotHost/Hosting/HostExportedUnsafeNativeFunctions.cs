using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Tinkwell.Firmwareless.Vfs;

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

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NativeOpenDelegate(nint execEnv, nint pathPtr, int pathLen, uint mode, uint flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NativeCloseDelegate(nint execEnv, int handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NativeReadDelegate(nint execEnv, int handle, nint bufferPtr, uint bufferLen, int bytesToRead, uint flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NativeWriteDelegate(nint execEnv, int handle, nint bufferPtr, uint bufferLen, int bytesToWrite, uint flags);

    public void RegisterAll()
    {
        if (_instance is not null)
            throw new HostException($"{nameof(HostExportedUnsafeNativeFunctions)} is not used as singleton or {nameof(RegisterAll)}() has been called twice.");

        _instance = this;

        Wamr.RegisterNativeFunctions([
            Wamr.MakeNativeSymbol(nameof(abort), new NativeAbortDelegate(abort)),
            Wamr.MakeNativeSymbol(nameof(tw_log), new NativeLogDelegate(tw_log)),
            Wamr.MakeNativeSymbol(nameof(tw_mqtt_publish), new NativeMqttPublishDelegate(tw_mqtt_publish)),
            Wamr.MakeNativeSymbol(nameof(tw_open), new NativeOpenDelegate(tw_open)),
            Wamr.MakeNativeSymbol(nameof(tw_close), new NativeCloseDelegate(tw_close)),
            Wamr.MakeNativeSymbol(nameof(tw_read), new NativeReadDelegate(tw_read)),
            Wamr.MakeNativeSymbol(nameof(tw_write), new NativeWriteDelegate(tw_write))
        ]);
    }

    private static HostExportedUnsafeNativeFunctions? _instance;
    private readonly ILogger<HostExportedUnsafeNativeFunctions> _logger = logger;
    private readonly IHostExportedFunctions _hostExportedFunctions = hostExportedFunctions;

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Match exported name")]
    private static void abort(nint execEnv, nint messagePtr, nint fileNamePtr, int line, int column)
    {
        // This function is not safe (messagePtr and fileNamePtr do not have a maximum length).
        // Unfortunately it's a function required by the AssemblyScript runtime (which we want to support).
        // HighlyUnsafeUtf8PtrToString() must be used with extreme caution. We accept this risk because a
        // malformed call will print process' memory to the log and nothing else (512 bytes at most) and
        // it'll cause the process to terminate anyway.
        Debug.Assert(_instance is not null);
        try
        {
            nint moduleInstance = Wamr.GetModuleInstanceFromExecEnvHandle(execEnv);
            string message = WasmMemory.HighlyUnsafeUtf8PtrToString(moduleInstance, messagePtr);
            string fileName = WasmMemory.HighlyUnsafeUtf8PtrToString(moduleInstance, fileNamePtr);

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
        return TryCallHostFunction(nameof(tw_log), () =>
        {
            nint moduleInstance = Wamr.GetModuleInstanceFromExecEnvHandle(execEnv);
            string topic = WasmMemory.Utf8PtrToString(moduleInstance, topicPtr, topicLen);
            string message = WasmMemory.Utf8PtrToString(moduleInstance, messagePtr, messageLen);

            _instance._hostExportedFunctions.Log(severity, topic, message);
            return 0;
        });
    }

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Match exported name")]
    private static int tw_mqtt_publish(nint execEnv, nint topicPtr, int topicLen, nint payloadPtr, int payloadLen)
    {
        Debug.Assert(_instance is not null);
        return TryCallHostFunction(nameof(tw_mqtt_publish), () =>
        {
            nint moduleInstance = Wamr.GetModuleInstanceFromExecEnvHandle(execEnv);
            string topic = WasmMemory.Utf8PtrToString(moduleInstance, topicPtr, topicLen);
            string payload = WasmMemory.Utf8PtrToString(moduleInstance, payloadPtr, payloadLen);

            _instance._hostExportedFunctions.PublishMqttMessage(topic, payload);
            return 0;
        });
    }

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Match exported name")]
    private static int tw_open(nint execEnv, nint pathPtr, int pathLen, uint mode, uint flags)
    {
        Debug.Assert(_instance is not null);
        return TryCallHostFunction(nameof(tw_open), () =>
        {
            nint moduleInstance = Wamr.GetModuleInstanceFromExecEnvHandle(execEnv);
            string path = WasmMemory.Utf8PtrToString(moduleInstance, pathPtr, pathLen);

            return _instance._hostExportedFunctions.OpenFile(
                path,
                CheckEnum((OpenMode)mode),
                CheckEnum((OpenFlags)flags)
            );
        });
    }

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Match exported name")]
    private static int tw_close(nint execEnv, int handle)
    {
        Debug.Assert(_instance is not null);
        return TryCallHostFunction(nameof(tw_close), () =>
        {
            _instance._hostExportedFunctions.CloseFile(handle);
            return (int)WasmErrorCode.Ok;
        });
    }

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Match exported name")]
    private static int tw_read(nint execEnv, int handle, nint bufferPtr, uint bufferLength, int bytesToRead, uint flags)
    {
        Debug.Assert(_instance is not null);
        return TryCallHostFunction(nameof(tw_read), () =>
        {
            if (bytesToRead < 0)
                throw new ArgumentOutOfRangeException(nameof(bytesToRead), bytesToRead, "Number of bytes cannot be less than zero.");

            if (bytesToRead > bufferLength)
                throw new ArgumentOutOfRangeException(nameof(bytesToRead), bytesToRead, "Source buffer is too small.");

            unsafe
            {                
                nint moduleInstance = Wamr.GetModuleInstanceFromExecEnvHandle(execEnv);
                var ptr = WasmMemory.MapAppAddressRangeToNative(moduleInstance, bufferPtr, bytesToRead);
                var buffer = new Span<byte>(ptr.ToPointer(), bytesToRead);

                return _instance._hostExportedFunctions.ReadFromFile(
                    handle,
                    buffer,
                    CheckEnum((ReadFlags)flags)
                );
            }
        });
    }

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Match exported name")]
    private static int tw_write(nint execEnv, int handle, nint bufferPtr, uint bufferLength, int bytesToWrite, uint flags)
    {
        Debug.Assert(_instance is not null);
        return TryCallHostFunction(nameof(tw_write), () =>
        {
            if (bytesToWrite < 0)
                throw new ArgumentOutOfRangeException(nameof(bytesToWrite), bytesToWrite, "Number of bytes cannot be less than zero.");

            if (bytesToWrite > bufferLength)
                throw new ArgumentOutOfRangeException(nameof(bytesToWrite), bytesToWrite, "Destination buffer is too small.");

            unsafe
            {
                nint moduleInstance = Wamr.GetModuleInstanceFromExecEnvHandle(execEnv);
                var ptr = WasmMemory.MapAppAddressRangeToNative(moduleInstance, bufferPtr, bytesToWrite);
                var buffer = new Span<byte>(ptr.ToPointer(), bytesToWrite);

                return _instance._hostExportedFunctions.WriteToFile(
                    handle,
                    buffer,
                    CheckEnum((WriteFlags)flags)
                );
            }
        });
    }

    private static T CheckEnum<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined<T>(value))
            throw new ArgumentException($"Value {value} is not a valid {typeof(T).Name} enumeration value.");

        return value;
    }

    private static int TryCallHostFunction(string functionName, Func<int> call)
    {
        try
        {
            return call();
        }
        catch (ArgumentOutOfRangeException e)
        {
            return LogException(functionName, e, WasmErrorCode.ArgumentOutOfRange);
        }
        catch (ArgumentException e)
        {
            return LogException(functionName, e, WasmErrorCode.InvalidArgument);
        }
        catch (FormatException e)
        {
            return LogException(functionName, e, WasmErrorCode.ArgumentInvalidFormat);
        }
        catch (OutOfMemoryException e)
        {
            return LogException(functionName, e, WasmErrorCode.OutOfMemory);
        }
        catch (IOException e)
        {
            return LogException(functionName, e, WasmErrorCode.Io);
        }
        catch (UnauthorizedAccessException e)
        {
            return LogException(functionName, e, WasmErrorCode.NoAccess);
        }
        catch (NotSupportedException e)
        {
            return LogException(functionName, e, WasmErrorCode.NotSupported);
        }
        catch (NotImplementedException e)
        {
            return LogException(functionName, e, WasmErrorCode.NotImplemented);
        }
        catch (HostException e)
        {
            return LogException(functionName, e, WasmErrorCode.Host);
        }
        catch (VfsAccessException e)
        {
            return LogException(functionName, e, WasmErrorCode.NoAccess);
        }
        catch (VfsAmbiguousPathException e)
        {
            return LogException(functionName, e, WasmErrorCode.InvalidArgument);
        }
        catch (VfsNotFoundException e)
        {
            return LogException(functionName, e, WasmErrorCode.NotFound);
        }
        catch (VfsOutOfResourcesException e)
        {
            return LogException(functionName, e, WasmErrorCode.OutOfMemory);
        }
        catch (Exception e)
        {
            return LogException(functionName, e, WasmErrorCode.Generic);
        }

        static int LogException(string functionName, Exception exception, WasmErrorCode error)
        {
            Debug.Assert(_instance is not null);
            _instance._logger.LogWarning(exception,
                "Error {Error} ({ExceptionType}) when calling host function {Function}(): {Message}",
                error, exception.GetType().Name, functionName, exception.Message);

            return (int)error;
        }
    }
}
