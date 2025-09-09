# Firmwareless Host

```mermaid
   graph LR;
      Repository -- "[firmlet (native)]" --> Hub;
      Hub -- "fetch" --> Repository;
      Hub -- "query" --> Device;
      Device -- "[manifest]" --> Hub;
      Vendor -- "publish firmlet (WASM)" --> Repository;

      style Hub fill:#ff9999,stroke:#333,stroke-width:2px
```

## See Also

* [Overall Architecture](./Documentation/Overall_Architecture.md)
* [System Diagrams](./Documentation/System_Diagrams.md)
* [Threat Modeling](./Documentation/Threat_Modeling.md)