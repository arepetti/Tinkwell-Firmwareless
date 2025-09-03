# System Diagrams

This document contains diagrams illustrating the static and dynamic relationship between system components.

---

## 1. Component Diagram

This diagram shows the high-level static components of the system, their relationships, and the boundaries of the container and host machine.

```mermaid
graph TD
    subgraph HostMachine[Host Machine]
        WasmHost("WasmHost Process");
        DockerEngine("Docker Engine");
        CacheDir("</> Shared Cache<br/>(Firmware Packages)");
    end

    WasmHost -- "Manages Packages" --> CacheDir;
    WasmHost -- "Starts Container" --> DockerEngine;

    subgraph DockerContainer[Docker Container]
        direction LR
        Coordinator("<b>WamrAotHost</b><br/>(Coordinator Mode)");

        subgraph HostProcesses[Host Processes]
            direction TB
            Host1("<b>WamrAotHost</b><br/>(Host 1)");
            Host2("<b>WamrAotHost</b><br/>(Host 2)");
            MoreHosts("...");
        end

        Coordinator -- "Reads From" --> CacheDir;
        Coordinator -- "Spawns" --> Host1;
        Coordinator -- "Spawns" --> Host2;
        Coordinator -- "Spawns" --> MoreHosts;

        Host1 <--> Coordinator;
        Host2 <--> Coordinator;
        MoreHosts <--> Coordinator;
    end

```

---

## 2. Sequence Diagram: Process Startup & Restart

This diagram shows the dynamic interaction over time between the Coordinator and a single Host process, including the key scenario where a host exits unexpectedly and is restarted by the coordinator's supervision logic.

```mermaid
sequenceDiagram
    participant C as Coordinator
    participant H as Host Process

    C->>+H: Spawns Process(firmware)
    Note over H: Host starts up
    H->>+C: Connects to Pipe
    H-->>C: NotifyAsync(RegisterClient)
    C->>C: Mark Host as Ready

    loop Health Monitoring
        C->>C: MonitorProcesses() Timer Tick
    end

    Note over H: ...time passes...

    H--xH: Process Exits Unexpectedly
    Note over C: OnProcessExited event fires
    C->>C: Log exit & calculate backoff delay
    Note over C: await Task.Delay(...)

    C->>+H: Spawns Process (Restart)
    H->>+C: Connects to Pipe
    H-->>C: NotifyAsync(RegisterClient)
    C->>C: Mark Host as Ready
```
