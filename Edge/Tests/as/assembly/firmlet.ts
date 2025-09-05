// This is where the firmlet logic resides

import { Reason, log, events, mqtt } from "./tinkwell";

events.onStart((reason: Reason) => {
    log.info("AssemblyScript example is ready and working");
    mqtt.publish("some_topic", "some_payload");
});
