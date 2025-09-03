// An higher level interface to interact with Tinkwell Firmwareless services

import { tw_log, tw_mqtt_publish } from "../env";

export enum Reason { Lifecycle = 0 };

type VoidCallback = () => void;
type ReasonCallback = (reason: Reason) => void;
type OnMessageReceivedCallback = (topic: string, payload: string) => void;

let firmletInstanceId: string = "";
let initializeCallbacks: VoidCallback[] = [];
let disposeCallbacks: ReasonCallback[] = [];
let startCallbacks: ReasonCallback[] = [];
let messageReceivedCallbacks: OnMessageReceivedCallback[] = [];

export namespace me {
  export function getInstanceId(): string {
    return firmletInstanceId;
  }
}

export namespace log {
  export function error(message: string): void {
    const topicBuf = String.UTF8.encode(firmletInstanceId, true);
    const messageBuf = String.UTF8.encode(message, true);
    tw_log(0, changetype<usize>(topicBuf), topicBuf.byteLength, changetype<usize>(messageBuf), messageBuf.byteLength);
  }
  export function warning(message: string): void {
    const topicBuf = String.UTF8.encode(firmletInstanceId, true);
    const messageBuf = String.UTF8.encode(message, true);
    tw_log(1, changetype<usize>(topicBuf), topicBuf.byteLength, changetype<usize>(messageBuf), messageBuf.byteLength);
  }
  export function info(message: string): void {
    const topicBuf = String.UTF8.encode(firmletInstanceId, true);
    const messageBuf = String.UTF8.encode(message, true);
    tw_log(2, changetype<usize>(topicBuf), topicBuf.byteLength, changetype<usize>(messageBuf), messageBuf.byteLength);
  }
  export function debug(message: string): void {
    const topicBuf = String.UTF8.encode(firmletInstanceId, true);
    const messageBuf = String.UTF8.encode(message, true);
    tw_log(3, changetype<usize>(topicBuf), topicBuf.byteLength, changetype<usize>(messageBuf), messageBuf.byteLength);
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

export namespace __internal {
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
  export function messageReceived(topicPtr: usize, topicLen: i32, payloadPtr: usize, payloadLen: i32): void {
    const topic = String.UTF8.decodeUnsafe(topicPtr, topicLen, true);
    const payload = String.UTF8.decodeUnsafe(payloadPtr, payloadLen, true);

    log.debug(`Received topic ${topic} (subscribers: ${messageReceivedCallbacks.length})`);
    for (let i = 0; i < messageReceivedCallbacks.length; ++i) {
      messageReceivedCallbacks[i](topic, payload);
    }
  }
}