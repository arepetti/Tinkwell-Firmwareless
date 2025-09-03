// This is where the firmlet logic resides

import { Reason, log, events } from "./tinkwell";

events.onStart((reason: Reason) => {
    log.info("AssemblyScript example is ready and working");
});
