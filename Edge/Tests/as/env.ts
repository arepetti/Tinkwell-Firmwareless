// Functions exported by the host
export declare function tw_mqtt_publish(topicPtr: usize, topicLen: i32, payloadPtr: usize, payloadLen: i32): i32;
export declare function tw_log(severity: i32, topicPtr: usize, topicLen: i32, messagePtr: usize, messageLen: i32): i32;