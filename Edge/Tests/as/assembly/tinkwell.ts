// An higher level interface to interact with Tinkwell Firmwareless services

import { tw_log, tw_mqtt_publish, tw_open, tw_close, tw_read, tw_write } from "../env";

export enum Reason { Lifecycle = 0 };

type VoidCallback = () => void;
type ReasonCallback = (reason: Reason) => void;
type OnMessageReceivedCallback = (topic: string, payload: string) => void;

let firmletInstanceId: string = "";
let initializeCallbacks: VoidCallback[] = [];
let disposeCallbacks: ReasonCallback[] = [];
let startCallbacks: ReasonCallback[] = [];
let configChangedCallbacks: VoidCallback[] = [];
let messageReceivedCallbacks: OnMessageReceivedCallback[] = [];

function handleErrorCode(errorCode: i32) {
  switch (errorCode)
  {
    case errors.ERROR_INVALID_ARGUMENT:
      throw new errors.TinkwellError("Invalid argument.", errorCode);
    case errors.ERROR_ARGUMENT_OUT_OF_RANGE:
      throw new errors.TinkwellError("Argument out of range.", errorCode);
    case errors.ERROR_ARGUMENT_INVALID_FORMAT:
      throw new errors.TinkwellError("Invalid argument format/data.", errorCode);
    case errors.ERROR_OUT_OF_MEMORY:
      throw new errors.TinkwellError("Out of memory or resources.", errorCode);
    case errors.ERROR_IO:
      throw new errors.TinkwellError("I/O error.", errorCode);
    case errors.ERROR_NO_ACCESS:
      throw new errors.TinkwellError("You do not have permissions to access this resource.", errorCode);
    case errors.ERROR_NOT_SUPPORTED:
    case errors.ERROR_NOT_IMPLEMENTED:
      throw new errors.TinkwellError("Operation not supported by this resource.", errorCode);
    case errors.ERROR_NOT_FOUND:
      throw new errors.TinkwellError("Resource not found.", errorCode);
    default:
      throw new errors.TinkwellError("Operation failed", errorCode);
  }
}

export namespace me {
  export function getInstanceId(): string {
    return firmletInstanceId;
  }

  export function probeConfigValue(name: string): bool {
    return io.probe("/config/" + name, io.OpenMode.Read);
  }

  export function readConfigValue<T>(name: string): T {
    return io.readValue<T>("/config/" + name);
  }

  export function probeStatusValue(name: string): bool {
    return io.probe("/status/" + name, io.OpenMode.Read);
  }

  export function readStatusValue<T>(name: string): T {
    return io.readValue<T>("/status/" + name);
  }

  export function writeStatusValue<T>(name: string, value: T) {
    io.writeValue("/status/" + name, value);
  }

  export function sendCommand(command: string, payload: string = "") {
      io.writeValue("/dev/me/" + command, payload);
  }
}

export namespace log {
  const LOG_ERROR = 0;
  const LOG_WARNING = 1;
  const LOG_INFO = 2;
  const LOG_DEBUG = 3;

  export function error(message: string): void {
    const topicBuf = String.UTF8.encode(firmletInstanceId, true);
    const messageBuf = String.UTF8.encode(message, true);
    tw_log(LOG_ERROR, changetype<usize>(topicBuf), topicBuf.byteLength, changetype<usize>(messageBuf), messageBuf.byteLength);
  }
  export function warning(message: string): void {
    const topicBuf = String.UTF8.encode(firmletInstanceId, true);
    const messageBuf = String.UTF8.encode(message, true);
    tw_log(LOG_WARNING, changetype<usize>(topicBuf), topicBuf.byteLength, changetype<usize>(messageBuf), messageBuf.byteLength);
  }
  export function info(message: string): void {
    const topicBuf = String.UTF8.encode(firmletInstanceId, true);
    const messageBuf = String.UTF8.encode(message, true);
    tw_log(LOG_INFO, changetype<usize>(topicBuf), topicBuf.byteLength, changetype<usize>(messageBuf), messageBuf.byteLength);
  }
  export function debug(message: string): void {
    const topicBuf = String.UTF8.encode(firmletInstanceId, true);
    const messageBuf = String.UTF8.encode(message, true);
    tw_log(LOG_DEBUG, changetype<usize>(topicBuf), topicBuf.byteLength, changetype<usize>(messageBuf), messageBuf.byteLength);
  }
}

export namespace events {
  export function onInitialize(handler: VoidCallback): void {
    initializeCallbacks.push(handler);
  }
  export function onDispose(handler: ReasonCallback): void {
    disposeCallbacks.push(handler);
  }
  export function onStart(handler: ReasonCallback): void {
    startCallbacks.push(handler);
  }
  export function onConfigChanged(handler: VoidCallback): void {
    configChangedCallbacks.push(handler);
  }
  export function onMessageReceived(handler: OnMessageReceivedCallback): void {
    messageReceivedCallbacks.push(handler);
  }
}

export namespace mqtt {
  export function publish(topic: string, payload: string): void {
    const topicBuf = String.UTF8.encode(topic, true);
    const payloadBuf = String.UTF8.encode(payload, true);
    tw_mqtt_publish(changetype<usize>(topicBuf), topicBuf.byteLength, changetype<usize>(payloadBuf), payloadBuf.byteLength);
  }
}

export namespace binding {
  export function initialize(idPtr: usize, idLen: i32): void {
    const id = String.UTF8.decodeUnsafe(idPtr, idLen, true);

    firmletInstanceId = id;

    log.debug(`Initializing (subscribers: ${initializeCallbacks.length})...`);
    for (let i = 0; i < initializeCallbacks.length; ++i) {
      initializeCallbacks[i]();
    }
  }
  export function dispose(reason: i32): void {
    log.debug(`Disposing (reason: ${reason}, subscribers: ${disposeCallbacks.length})...`);
    for (let i = 0; i < disposeCallbacks.length; ++i) {
      disposeCallbacks[i](<Reason>reason);
    }
  }
  export function start(reason: i32): void {
    log.debug(`Starting (reason: ${reason}, subscribers: ${startCallbacks.length})...`);
    for (let i = 0; i < startCallbacks.length; ++i) {
      startCallbacks[i](<Reason>reason);
    }
    log.debug("Started...");
  }
  export function configChanged(reason: i32): void {
    log.debug("Config changed");
    for (let i = 0; i < configChangedCallbacks.length; ++i) {
      configChangedCallbacks[i]();
    }
  }
  export function messageReceived(topicPtr: usize, topicLen: i32, payloadPtr: usize, payloadLen: i32): void {
    const topic = String.UTF8.decodeUnsafe(topicPtr, topicLen, true);
    const payload = String.UTF8.decodeUnsafe(payloadPtr, payloadLen, true);

    log.debug(`Received topic ${topic} (subscribers: ${messageReceivedCallbacks.length})`);
    for (let i = 0; i < messageReceivedCallbacks.length; ++i) {
      messageReceivedCallbacks[i](topic, payload);
    }
  }
}

export namespace errors {
  export const ERROR_GENERIC: i32 = -1;
  export const ERROR_HOST: i32 = -2;
  export const ERROR_OUT_OF_MEMORY: i32 = -3;
  export const ERROR_IO: i32 = -4;
  export const ERROR_NOT_SUPPORTED: i32 = -10;
  export const ERROR_NOT_IMPLEMENTED: i32 = -11;
  export const ERROR_INVALID_ARGUMENT: i32 = -20;
  export const ERROR_ARGUMENT_OUT_OF_RANGE: i32 = -21;
  export const ERROR_ARGUMENT_INVALID_FORMAT: i32 = -22;
  export const ERROR_NOT_FOUND: i32 = -31;
  export const ERROR_NO_ACCESS: i32 = -32;

  export class TinkwellError extends Error {
    private readonly _code: i32;
    public constructor(message: string, code: i32) {
      super(message);
      this._code = code;
    }
    public get code(): i32 {
      return this._code;
    }
  }
}

export namespace io {
  export type Handle = i32;

  export enum OpenMode {
    Read = 0,
    Write = 1
  }

  export enum OpenFlags {
    None = 0,
    Probe = 1
  }

  export enum ReadFlags {
    None = 0
  }
  
  export enum WriteFlags {
    None = 0
  }

  export function open(name: string, mode: OpenMode, flags: OpenFlags = OpenFlags.None): Handle {
    const nameBuf = String.UTF8.encode(name, true);
    const handleOrErrorCode = tw_open(changetype<usize>(nameBuf), nameBuf.byteLength, <u32>mode, <u32>flags);

    if (handleOrErrorCode < 0)
      handleErrorCode(handleOrErrorCode);

    return handleOrErrorCode;
  }

  export function probe(name: string, mode: OpenMode, flags: OpenFlags = OpenFlags.Probe): bool {
    const nameBuf = String.UTF8.encode(name, true);
    const handleOrErrorCode = tw_open(changetype<usize>(nameBuf), nameBuf.byteLength, <u32>mode, <u32>flags);

    if (handleOrErrorCode < 0)
      return false;

    return tw_close(handleOrErrorCode) >= 0;
  }
  
  export function close(handle: Handle): void {
    const result = tw_close(handle);
    if (result < 0)
      handleErrorCode(result);
  }

  export function read(handle: Handle, buffer: ArrayBuffer, count?: i32, flags: ReadFlags = ReadFlags.None): i32 {
    const bytesToRead = Math.min(buffer.byteLength, count ?? buffer.byteLength);
    const bytesRead = tw_read(handle, changetype<usize>(buffer), buffer.byteLength, bytesToRead, flags);

    if (bytesRead < 0)
      handleErrorCode(bytesRead);

    return bytesRead;
  }

  export function write(handle: Handle, buffer: ArrayBuffer, count?: i32, flags: WriteFlags = WriteFlags.None): i32 {
    const bytesToWrite = Math.min(buffer.byteLength, count ?? buffer.byteLength);
    const bytesWritten = tw_write(handle, changetype<usize>(buffer), buffer.byteLength, bytesToWrite, flags);

    if (bytesWritten < 0)
      handleErrorCode(bytesWritten);

    return bytesWritten;
  }
  
  export function readValue<T>(name: string): T {
    const nameBuf = String.UTF8.encode(name, true);
    const handle = tw_open(changetype<usize>(nameBuf), nameBuf.byteLength, <u32>OpenMode.Read, 0);
    if (handle < 0)
      handleErrorCode(handle);

    const buffer = new ArrayBuffer(sizeof<T>());
    const bytesRead = tw_read(handle, changetype<usize>(buffer), buffer.byteLength, buffer.byteLength, 0);
    tw_close(handle);

    if (bytesRead < 0)
      handleErrorCode(bytesRead);

    return readBytesAs<T>(buffer);
  }

  export function writeValue<T>(name: string, value: T): i32 {
    const nameBuf = String.UTF8.encode(name, true);
    const handle = tw_open(changetype<usize>(nameBuf), nameBuf.byteLength, <u32>OpenMode.Write, 0);
    if (handle < 0)
      handleErrorCode(handle);

    const buffer = new ArrayBuffer(sizeof<T>());
    writeBytesAs(buffer, value);
  
    const bytesWritten = tw_write(handle, changetype<usize>(buffer), buffer.byteLength, buffer.byteLength, 0);
    tw_close(handle);

    if (bytesWritten < 0)
      handleErrorCode(bytesWritten);

    return bytesWritten;
  }

  function readBytesAs<T>(buffer: ArrayBuffer, littleEndian: bool = true): T {
    const view = new DataView(buffer);

    if (idof<T> === idof<bool>) {
      return <T>(view.getInt8(0) != 0);
    }

    if (isInteger<T>()) {
      switch (idof<T>()) {
        case idof<i8>():  return changetype<T>(view.getInt8(0));
        case idof<u8>():  return changetype<T>(view.getUint8(0));
        case idof<i16>(): return changetype<T>(view.getInt16(0, littleEndian));
        case idof<u16>(): return changetype<T>(view.getUint16(0, littleEndian));
        case idof<i32>(): return changetype<T>(view.getInt32(0, littleEndian));
        case idof<u32>(): return changetype<T>(view.getUint32(0, littleEndian));
        case idof<i64>(): return changetype<T>(view.getInt64(0, littleEndian));
        case idof<u64>(): return changetype<T>(view.getUint64(0, littleEndian));
        default: break;
      }
    } else if (isFloat<T>()) {
      switch (idof<T>()) {
        case idof<f32>(): return changetype<T>(view.getFloat32(0, littleEndian));
        case idof<f64>(): return changetype<T>(view.getFloat64(0, littleEndian));
        default: break;
      }
    }

    throw new Error("Unsupported data type: " + idof<T>());
  }

  function writeBytesAs<T>(buffer: ArrayBuffer, value: T, littleEndian: bool = true): void {
    const view = new DataView(buffer);

    if (idof<T> === idof<bool>) {
      view.setInt8(0, value === true ? 1 : 0);
      return;
    }

    if (isInteger<T>()) {
      switch (idof<T>()) {
        case idof<i8>():  view.setInt8(0, value as i8); break;
        case idof<u8>():  view.setUint8(0, value as u8); break;
        case idof<i16>(): view.setInt16(0, value as i16, littleEndian); break;
        case idof<u16>(): view.setUint16(0, value as u16, littleEndian); break;
        case idof<i32>(): view.setInt32(0, value as i32, littleEndian); break;
        case idof<u32>(): view.setUint32(0, value as u32, littleEndian); break;
        case idof<i64>(): view.setInt64(0, value as i64, littleEndian); break;
        case idof<u64>(): view.setUint64(0, value as u64, littleEndian); break;
        default: throw new Error("Unsupported integer type.");
      }
    } else if (isFloat<T>()) {
      switch (idof<T>()) {
        case idof<f32>(): view.setFloat32(0, value as f32, littleEndian); break;
        case idof<f64>(): view.setFloat64(0, value as f64, littleEndian); break;
        default: throw new Error("Unsupported float type.");
      }
    } else {
      throw new Error("Unsupported data type: " + idof<T>());
    }
  }
}

export namespace scheduler {
  export function getCurrentTime(): Date {
    return new Date(io.readValue<i64>("/dev/clock"));
  }

  export function wait(): boolean {
    return io.readValue<i32>("/dev/scheduler") >= 0;
  }
}