// This file contains all the entry-points called by the host

import { binding } from "./tinkwell";
import "./firmlet";

export function _initialize(idPtr: usize, idLen: i32): void {
  binding.initialize(idPtr, idLen);
}

export function _start(reason: i32): void {
  binding.start(reason);
}

export function _dispose(reason: i32): void {
  binding.dispose(reason);
}

export function _on_config_changed(reason: i32): void {
  binding.configChanged(reason);
}

export function _on_message_received(topicPtr: usize, topicLen: i32, payloadPtr: usize, payloadLen: i32): void {
  binding.messageReceived(topicPtr, topicLen, payloadPtr, payloadLen);
}
