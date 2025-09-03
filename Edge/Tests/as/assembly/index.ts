// This file contains all the entry-points called by the host

import { __internal } from "./tinkwell";
import "./firmlet";

export function _initialize(idPtr: usize, idLen: i32): void {
  __internal.initialize(idPtr, idLen);
}

export function _start(reason: i32): void {
  __internal.start(reason);
}

export function _dispose(reason: i32): void {
  __internal.dispose(reason);
}

export function _on_message_received(topicPtr: usize, topicLen: i32, payloadPtr: usize, payloadLen: i32): void {
  __internal.messageReceived(topicPtr, topicLen, payloadPtr, payloadLen);
}
