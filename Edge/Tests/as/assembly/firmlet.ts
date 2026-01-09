// This is where the firmlet logic resides

import { Reason, log, events, me, scheduler } from "./tinkwell";

events.onStart((reason: Reason) => {
    log.info("Thermostat is ready and running");

    if (reason === Reason.Lifecycle)
        Device.setFurnaceStatus(false);

    while (scheduler.wait()) {
        const target = Config.getTargetTemperature();
        const current = Device.getCurrentTemperature();
        log.info(`Target: ${target} (slot #${Config.getSchedulingSlot()}), current: ${current}.`);

        // If we don't have a target temperature then the heating is off; if we don't
        // know the current temperature then we wait until it's available (just started?)
        if (target === undefined || current === undefined) {
            Device.setFurnaceStatus(false);
        } else {
            Device.setFurnaceStatus(current < target);
        }
    }
});

class Device {
    public static getCurrentTemperature(): f32 | undefined {
        if (me.probeStatusValue("temperature"))
            return me.readStatusValue<f32>("temperature");

        // If the current temperature is unknown then we ask the device
        // to read it, it'll be available for the next iteration.
        me.sendCommand("read");

        return undefined;
    }

    public static setFurnaceStatus(active: bool) {
        const isFurnaceActive = () => {
            if (me.probeStatusValue("furnace"))
                return me.readStatusValue<bool>("furnace");

            return undefined;
        }

        if (isFurnaceActive() !== active) {
            me.writeStatusValue("furnace", active);
            me.sendCommand(active ? "on" : "off");
        }
    }
}

class Config {
    public static getTargetTemperature(): f32 | undefined {
        const key = "temperature/" + Config.getSchedulingSlot();
        if (me.probeConfigValue(key))
            return me.readConfigValue<f32>(key);

        return undefined;
    }

    public static getSchedulingSlot(): u8 {
        const now = scheduler.getCurrentTime();
        const minutesSinceMidnight = now.getUTCHours() * 60 + now.getUTCMinutes();
        return Math.floor(minutesSinceMidnight / 30);
    }
}
