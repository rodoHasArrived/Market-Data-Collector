# C4 Diagrams

## Rendered exports (SVG/PNG)

These are pre-rendered exports for environments that don't support Mermaid rendering.

- Level 1: System Context  
  - SVG: `docs/diagrams/c4-level1-context.svg`  
  - PNG: `docs/diagrams/c4-level1-context.png`

- Level 2: Containers  
  - SVG: `docs/diagrams/c4-level2-containers.svg`  
  - PNG: `docs/diagrams/c4-level2-containers.png`

- Level 3: Components  
  - SVG: `docs/diagrams/c4-level3-components.svg`  
  - PNG: `docs/diagrams/c4-level3-components.png`

---

These diagrams describe the system using the C4 model:
- **Level 1**: System Context
- **Level 2**: Container Diagram
- **Level 3**: Component Diagram (Collector runtime)

> Notes:
> - Mermaid diagrams render on GitHub and in many markdown viewers.
> - For DocFX, Mermaid rendering depends on theme/extension. If Mermaid isn’t rendered, treat these as source diagrams and export to SVG/PNG later.

---

## Level 1 — System Context (C4)

```mermaid
flowchart LR
    IB[Interactive Brokers\nTWS/Gateway]:::ext
    OPR[Operator]:::person
    UI[IBDataCollector.Ui\nDashboard]:::container
    COL[IB Data Collector\nService]:::system
    DISK[(Local Storage\nJSONL / Gzip)]:::store

    OPR --> UI
    UI <--> DISK
    UI --> COL
    IB --> COL
    COL --> DISK

classDef person fill:#fff,stroke:#333,stroke-width:1px;
classDef ext fill:#f8f8f8,stroke:#333,stroke-dasharray: 4 2;
classDef container fill:#e8f4ff,stroke:#2b6cb0,stroke-width:1px;
classDef system fill:#e6fffa,stroke:#2c7a7b,stroke-width:1px;
classDef store fill:#fff5f5,stroke:#c53030,stroke-width:1px;
```

---

## Level 2 — Containers (C4)

```mermaid
flowchart TB
    subgraph C[IB Data Collector (Process)]
        APP[Application Layer\nProgram/ConfigWatcher/SubscriptionManager]:::container
        DOM[Domain Layer\nCollectors + Models]:::container
        PIPE[Event Pipeline\nIMarketEventBus/Routed Fanout]:::container
        STOR[Storage\nJsonlStorageSink/Policy]:::container
        INFRA[Infrastructure (IB)\nConnectionManager/Router/ContractFactory]:::container
    end

    IB[Interactive Brokers\nTWS/Gateway]:::ext
    DISK[(Filesystem\n./data)]:::store
    UI[ASP.NET UI\nIBDataCollector.Ui]:::container
    OPR[Operator]:::person

    OPR --> UI
    UI <--> DISK

    IB --> INFRA
    INFRA --> DOM
    DOM --> PIPE
    PIPE --> STOR
    STOR --> DISK

    APP --> INFRA
    APP --> DOM
    APP --> PIPE
    APP --> STOR

classDef person fill:#fff,stroke:#333,stroke-width:1px;
classDef ext fill:#f8f8f8,stroke:#333,stroke-dasharray: 4 2;
classDef container fill:#e8f4ff,stroke:#2b6cb0,stroke-width:1px;
classDef store fill:#fff5f5,stroke:#c53030,stroke-width:1px;
```

---

## Level 3 — Components (Collector Runtime)

```mermaid
flowchart LR
    subgraph INF[Infrastructure.IB]
        CONN[EnhancedIBConnectionManager\n(EWrapper)]:::component
        ROUTE[IBCallbackRouter]:::component
        FACT[ContractFactory]:::component
        CLIENT[IIBMarketDataClient\nIBMarketDataClient/NoOp]:::component
    end

    subgraph DOM[Domain]
        TD[TradeDataCollector]:::component
        MD[MarketDepthCollector]:::component
        MODELS[Models\nTrade/LOBSnapshot/Integrity]:::component
    end

    subgraph APP[Application]
        CW[ConfigWatcher]:::component
        SM[SubscriptionManager]:::component
        MET[Metrics/StatusWriter]:::component
    end

    subgraph PIPE[Pipeline/Storage]
        BUS[IMarketEventBus\nPer-symbol channels]:::component
        SINK[JsonlStorageSink]:::component
        POL[JsonlStoragePolicy]:::component
        FS[(Filesystem)]:::store
    end

    IB[IB TWS/Gateway]:::ext

    IB --> CONN --> ROUTE
    ROUTE --> TD
    ROUTE --> MD
    TD --> BUS
    MD --> BUS
    BUS --> SINK --> FS
    POL --> SINK

    CW --> SM
    SM --> CLIENT
    CLIENT --> FACT
    CLIENT --> CONN

    MET --> FS

classDef ext fill:#f8f8f8,stroke:#333,stroke-dasharray: 4 2;
classDef component fill:#f7fafc,stroke:#4a5568,stroke-width:1px;
classDef store fill:#fff5f5,stroke:#c53030,stroke-width:1px;
```
