import { tw_log, tw_mqtt_publish } from "../env";

type VoidCallback = () => void;
type OnMessageReceivedCallback = (topic: string, payload: string) => void;

let firmletInstanceId: string = "";
let initializeCallbacks: VoidCallback[] = [];
let disposeCallbacks: VoidCallback[] = [];
let startCallbacks: VoidCallback[] = [];
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
  export function onDispose(handler: VoidCallback): void {
    disposeCallbacks.push(handler);
  }
  export function onStart(handler: VoidCallback): void {
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
  export function initialize(id: string): void {
    firmletInstanceId = id;
    log.debug("Initializing...");
    for (let i = 0; i < initializeCallbacks.length; ++i) {
      initializeCallbacks[i]();
    }
  }
  export function dispose(): void {
    log.debug("Disposing...");
    for (let i = 0; i < disposeCallbacks.length; ++i) {
      disposeCallbacks[i]();
    }
  }
  export function start(): void {
    log.debug("Starting...");
    for (let i = 0; i < startCallbacks.length; ++i) {
      startCallbacks[i]();
    }
    log.debug("Started...");
  }
  export function messageReceived(topic: string, payload: string): void {
    for (let i = 0; i < messageReceivedCallbacks.length; ++i) {
      messageReceivedCallbacks[i](topic, payload);
    }
  }
}