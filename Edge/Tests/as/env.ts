// Functions exported by the host
export declare function tw_mqtt_publish(topicPtr: usize, topicLen: i32, payloadPtr: usize, payloadLen: i32): i32;
export declare function tw_log(severity: i32, topicPtr: usize, topicLen: i32, messagePtr: usize, messageLen: i32): i32;
export declare function tw_open(namePtr: usize, nameLen: i32, mode: u32, flags: u32): i32;
export declare function tw_close(handle: i32): i32;
export declare function tw_read(handle: i32, buffer: usize, bufferLength: u32, count: i32, flags: u32): i32;
export declare function tw_write(handle: i32, buffer: usize, bufferLength: u32, count: i32, flags: u32): i32;
