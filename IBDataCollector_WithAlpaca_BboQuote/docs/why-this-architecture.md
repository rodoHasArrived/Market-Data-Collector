# Why This Architecture (Non‑Engineer Explainer)

## What this program does
It records detailed market activity from Interactive Brokers:
- **Trades:** every print (tick-by-tick)
- **Depth:** the live order book (Level 2)

It stores that data in a format that can be audited, replayed, and analyzed.

---

## Why we split it into layers

### 1) Infrastructure (the “adapter”)
Interactive Brokers has its own API and quirks. We isolate all of that here so:
- we can swap providers later if needed
- the rest of the system stays stable
- data logic doesn’t become entangled with vendor specifics

### 2) Domain (the “brains”)
This is where we decide what the data *means*:
- keep an order book
- compute order-flow statistics
- detect when data is inconsistent or missing

This layer is written so it can be tested without connecting to IB.

### 3) Application (the “conductor”)
This is the part that runs the show:
- loads configuration
- starts/stops subscriptions
- hot reloads symbols without restarts
- writes status for monitoring/UI

### 4) Pipeline/Storage (the “transport and memory”)
We treat all recorded activity as a stream of standardized events and store them safely:
- bounded queues prevent runaway memory use
- data is written as append-only JSON lines so it’s easy to audit and replay

---

## Why this is safer and more “institutional”
- **Audit-friendly output:** append-only logs per symbol/type/day
- **Integrity events:** the system detects book corruption and stops rather than silently writing bad data
- **Hot reload with atomic writes:** reduces operational risk of restarts and partial configs
- **Provider independence:** the core logic remains correct even if IB integration changes

---

## What’s next for production maturity
- richer trade classification (buy vs sell)
- stronger monitoring/alerting
- automated recovery policies (resubscribe)
- CI test automation and release workflow

