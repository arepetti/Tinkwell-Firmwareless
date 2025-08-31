import { __internal } from "./tinkwell";

export function _initialize(idPtr: usize, idLen: i32): void {
  __internal.initialize(String.UTF8.decodeUnsafe(idPtr, idLen, true));
}

export function _start(): void {
  __internal.start();
}

export function _dispose(): void {
  __internal.dispose();
}

export function _on_message_received(topicPtr: usize, topicLen: i32, payloadPtr: usize, payloadLen: i32): void {
  const topic = String.UTF8.decodeUnsafe(topicPtr, topicLen, true);
  const payload = String.UTF8.decodeUnsafe(payloadPtr, payloadLen, true);
  __internal.messageReceived(topic, payload);
}
