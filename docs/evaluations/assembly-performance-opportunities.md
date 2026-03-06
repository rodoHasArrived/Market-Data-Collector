# Assembly-Level Performance Opportunities

This document identifies where hand-written assembly (or .NET hardware intrinsics, which compile to SIMD instructions) could materially improve performance in Market Data Collector.

## Executive Summary

Assembly is most valuable in this repository for **byte-level parsing/scanning hot paths** and **vectorizable numeric kernels**. It is unlikely to provide strong returns for orchestration logic, I/O waiting, or framework-heavy paths.

Highest-potential candidates:

1. JSONL newline and token scanning in memory-mapped replay.
2. Sequence-number extraction during data-quality scoring.
3. Bulk event-buffer copy/drain operations.
4. Optional checksum/compression fast paths (if profiling confirms CPU-bound behavior).

## Candidate Areas

## 1) Memory-mapped JSONL reader: newline scanning and UTF-8 handling

### Why this is a hot candidate

`MemoryMappedJsonlReader` currently scans each byte with a scalar loop and converts each discovered line using `Encoding.UTF8.GetString(...)`. That loop and conversion path execute for every input byte/line in large replay files.

### Where in code

- Chunk scan for `\n` delimiters and line slicing is in `ReadFileMemoryMappedAsync`.
- Batch processing repeatedly materializes strings prior to JSON deserialization.

### Assembly/SIMD opportunity

- Replace scalar newline search with vectorized delimiter scanning (`Vector128/256<byte>` compare-equals to `\n` + bitmask).
- Keep input as `ReadOnlySpan<byte>` longer and only decode when necessary.
- Consider SIMD classification for whitespace / `\r` trimming.

### Expected impact

- Strong upside for large uncompressed files where parsing is CPU-bound.
- Typical speedups for delimiter scanning kernels can be substantial (often 1.5x–4x for the scan portion), depending on file characteristics and CPU.

## 2) Data-quality sequence extraction: digit parsing from JSON lines

### Why this is a hot candidate

`DataQualityScoringService.ComputeSequenceScoreAsync` does repeated `IndexOf` + character-by-character loops to locate and parse numeric sequence fields from JSON text. This is branch-heavy and repeatedly executes over many lines.

### Where in code

- `"Sequence"`/`"sequence"` key search and numeric parse loops in `ComputeSequenceScoreAsync`.

### Assembly/SIMD opportunity

- Vectorized substring/key search in UTF-8 buffers instead of UTF-16 strings.
- SIMD-assisted digit run detection and conversion for contiguous numeric spans.
- Optionally parse with a small custom state machine over bytes (intrinsics-backed).

### Expected impact

- Moderate-to-high benefit if scoring many large files.
- Especially helpful if data-quality reporting runs frequently or on ingest-critical paths.

## 3) EventBuffer drain and remove-front behavior

### Why this is a candidate

`EventBuffer<T>.Drain(int maxCount)` currently uses `Take(...).ToList()` followed by `RemoveRange(0, count)`. Removing from the front of `List<T>` forces shifting remaining elements, which is O(n) and memory-copy heavy.

### Where in code

- `Drain(int maxCount)` in `EventBuffer<T>`.

### Assembly/SIMD opportunity

- Replace structure with ring buffer semantics to avoid front-shift copies.
- For contiguous copies, rely on vectorized memory moves (`Buffer.MemoryCopy` / runtime-optimized `memmove`) rather than per-item movement.

### Expected impact

- Moderate gains under high-frequency drain workloads.
- Better cache behavior and lower GC pressure than current front-removal pattern.

## 4) Checksum/compression pipeline (conditional)

### Why this is a candidate

Checksum and compression can become CPU hotspots in export/archival scenarios. Current checksum flow uses managed SHA-256 APIs, which may already leverage platform acceleration.

### Where in code

- `StorageChecksumService` methods computing SHA-256 on files/streams.

### Assembly/SIMD opportunity

- Only if profiling shows CPU-bound checksum throughput not saturating I/O.
- Consider hardware-intrinsics-backed fast path (SHA extensions) through optimized libraries rather than hand-written assembly.

### Expected impact

- Potentially low-to-moderate if already hardware-accelerated by runtime.
- Can be high only in sustained hash-heavy workflows where disk isn’t the bottleneck.

## 5) Event pipeline metrics/bookkeeping (low priority)

### Why it is lower ROI

`EventPipeline` work is mixed with channels, async scheduling, sink I/O, and logging. These are not ideal for hand assembly optimization.

### Where in code

- `TryPublish`, batching/flush logic, and WAL/sink interactions.

### Assembly/SIMD opportunity

- Minimal direct assembly value; prioritize algorithmic changes, batching, and lock/contention reduction.

## Prioritization

1. **MemoryMappedJsonlReader delimiter/token scan** (highest likely ROI).
2. **Sequence-score numeric extraction** (high ROI for analytics/report workloads).
3. **EventBuffer data structure redesign** (medium ROI, broad impact).
4. **Checksum/compression fast path** (profile-gated).
5. **EventPipeline assembly work** (generally not recommended).

## Practical Guidance

- Prefer **.NET hardware intrinsics** (`System.Runtime.Intrinsics`) over raw inline assembly for portability and maintainability.
- Gate fast paths with runtime CPU feature detection and keep scalar fallback.
- Benchmark with realistic files/symbol mixes before and after each optimization.
- Adopt profile-first rule: only optimize code confirmed hot by sampling/tracing.

## When *not* to use assembly here

Do not use assembly for:

- Business-rule orchestration and provider glue code.
- Async I/O control flow and retry policies.
- Most logging/configuration code.

For these areas, higher-level design changes will outperform assembly effort.

## Implementation Sketches (what the code would look like)

The examples below are intentionally partial sketches to show structure and integration points. They are not drop-in production code.

### Accuracy/completeness notes

- These sketches prioritize correctness-first patterns (bounds checks, scalar fallback) before micro-optimizing.
- Intrinsics paths should always have non-intrinsics fallback to preserve behavior on unsupported CPUs.
- Prefer validating each change with microbenchmarks + representative end-to-end replay benchmarks.

### A) Vectorized newline scan for `MemoryMappedJsonlReader`

A first implementation can stay fully in C# using intrinsics with scalar fallback:

```csharp
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

private static int FindNextNewline(ReadOnlySpan<byte> data, int start)
{
    if ((uint)start >= (uint)data.Length)
        return -1;

    if (Avx2.IsSupported)
    {
        var needle = Vector256.Create((byte)'\n');
        ref byte r0 = ref MemoryMarshal.GetReference(data);
        int i = start;
        int last = data.Length - Vector256<byte>.Count;

        while (i <= last)
        {
            var vec = Vector256.LoadUnsafe(ref Unsafe.Add(ref r0, i));
            var cmp = Avx2.CompareEqual(vec, needle);
            uint mask = (uint)Avx2.MoveMask(cmp);
            if (mask != 0)
                return i + BitOperations.TrailingZeroCount(mask);

            i += Vector256<byte>.Count;
        }

        for (; i < data.Length; i++)
            if (Unsafe.Add(ref r0, i) == (byte)'\n') return i;

        return -1;
    }

    // Portable fallback
    int idx = data[start..].IndexOf((byte)'\n');
    return idx >= 0 ? start + idx : -1;
}
```

Integration in `ReadFileMemoryMappedAsync` can be incremental:

1. keep current `List<string>` flow unchanged.
2. replace only delimiter search with `FindNextNewline(...)`.
3. benchmark.
4. then reduce UTF-8 decode churn by delaying `GetString(...)` calls.

### B) Byte-oriented sequence extraction for `ComputeSequenceScoreAsync`

Instead of scanning UTF-16 strings repeatedly, parse UTF-8 bytes with a compact state machine:

```csharp
private static bool TryExtractSequenceUtf8(ReadOnlySpan<byte> json, out long sequence)
{
    sequence = 0;

    ReadOnlySpan<byte> keyUpper = "\"Sequence\":"u8;
    ReadOnlySpan<byte> keyLower = "\"sequence\":"u8;

    int p = json.IndexOf(keyUpper);
    int keyLen = keyUpper.Length;
    if (p < 0)
    {
        p = json.IndexOf(keyLower);
        keyLen = keyLower.Length;
    }
    if (p < 0) return false;

    int i = p + keyLen;
    while (i < json.Length && (json[i] == (byte)' ' || json[i] == (byte)'\t')) i++;

    bool neg = i < json.Length && json[i] == (byte)'-';
    if (neg) i++;

    long value = 0;
    int digits = 0;

    while (i < json.Length)
    {
        uint d = (uint)(json[i] - (byte)'0');
        if (d > 9) break;

        // overflow-safe accumulation
        if (value > (long.MaxValue - d) / 10)
            return false;

        value = (value * 10) + (long)d;
        i++;
        digits++;
    }

    if (digits == 0) return false;
    sequence = neg ? -value : value;
    return true;
}
```

To keep the optimization meaningful, prefer reading bytes directly rather than doing per-line `Encoding.UTF8.GetBytes(line)` allocations.

### C) `EventBuffer<T>` redesign to ring buffer

Avoid `RemoveRange(0, count)` by keeping head/tail indices and growing capacity geometrically:

```csharp
public sealed class RingEventBuffer<T>
{
    private T?[] _buffer;
    private int _head;
    private int _tail;
    private int _count;

    public RingEventBuffer(int capacity = 1024)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _buffer = new T[capacity];
    }

    public int Count => _count;

    public void Add(T item)
    {
        if (_count == _buffer.Length)
            Grow();

        _buffer[_tail] = item;
        _tail = (_tail + 1) % _buffer.Length;
        _count++;
    }

    public List<T> Drain(int max)
    {
        if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));

        int n = Math.Min(max, _count);
        var result = new List<T>(n);

        for (int i = 0; i < n; i++)
        {
            result.Add(_buffer[_head]!);
            _buffer[_head] = default;
            _head = (_head + 1) % _buffer.Length;
        }

        _count -= n;
        return result;
    }

    private void Grow()
    {
        int newCap = checked(_buffer.Length * 2);
        var next = new T[newCap];

        int right = Math.Min(_buffer.Length - _head, _count);
        Array.Copy(_buffer, _head, next, 0, right);
        Array.Copy(_buffer, 0, next, right, _count - right);

        _buffer = next;
        _head = 0;
        _tail = _count;
    }
}
```

This removes front-shift copying entirely. If needed later, optimize `Drain` copy-out with two `Array.Copy` segments into a pre-sized array.

### D) Feature-gated dispatch pattern

Use one entry point with architecture checks:

```csharp
internal static class NewlineScanner
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Find(ReadOnlySpan<byte> data, int start)
    {
        if (Avx2.IsSupported) return FindAvx2(data, start);
        if (Sse2.IsSupported) return FindSse2(data, start);
        return FindScalar(data, start);
    }
}
```

This keeps maintenance manageable and allows deterministic tests per path.

### E) Benchmark harness shape

Use `BenchmarkDotNet` microbenchmarks before/after each optimization:

- `FindNextNewline`: scalar vs SSE2 vs AVX2 across realistic chunk sizes.
- Sequence parse: current string-based parser vs UTF-8 parser.
- Buffer drain: `List.RemoveRange` approach vs ring buffer.

Suggested benchmark dimensions:

- File profiles: small/medium/large JSONL chunks.
- Line lengths: short ticker events vs long depth updates.
- CPU targets: x64 AVX2, x64 SSE2-only, ARM64 (fallback; add AdvSimd path later).
- Data characteristics: newline density, invalid-json rate, and sequence-missing frequency.

### F) Validation checklist before merging code changes

1. **Correctness parity:** existing tests + new path-specific tests pass.
2. **Fallback safety:** force scalar path in tests and verify same results.
3. **Perf evidence:** benchmark wins on at least two representative datasets.
4. **No regression in memory:** allocations and GC pressure do not increase.
5. **Operational confidence:** replay/quality/report end-to-end timings improve measurably.
