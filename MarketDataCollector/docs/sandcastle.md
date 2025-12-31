# Sandcastle / SHFB (Optional)

If your environment prefers Sandcastle Help File Builder (SHFB):

## Option A: Use SHFB GUI
1. Install SHFB
2. Create a new SHFB project pointing at:
   - `src/IBDataCollector/IBDataCollector.csproj`
   - `src/IBDataCollector.Ui/IBDataCollector.Ui.csproj` (optional)
3. Ensure XML documentation output is enabled (already set in `IBDataCollector.csproj`)
4. Build to produce HTML help

## Option B: Use DocFX (recommended)
DocFX is already configured under `docs/docfx/` and is usually easier for CI and static hosting.
