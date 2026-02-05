# UI Redesign Proposal: WPF Desktop App

## Goals

- Reduce top-level navigation clutter while keeping the product feature-rich.
- Improve task flow by grouping related workflows into consolidated workspaces.
- Emphasize progressive disclosure and contextual actions.
- Provide a clean, scalable layout for dense data workflows.

## Scope & Assumptions

This proposal targets the current WPF desktop application and uses the existing page inventory as the source of truth for features that must remain accessible. The redesign focuses on information architecture (IA), navigation, and component patterns rather than re-implementing functionality.

## Consolidated Page Specs

### 1) Dashboard Workspace

**Purpose:** Provide at-a-glance system health, activity, and next-step actions.

**Pages consolidated:**
- DashboardPage
- ActivityLogPage
- NotificationCenterPage

**Key features:**
- Status summary cards (collection status, storage health, provider uptime)
- Recent activity feed (job status, alerts)
- Notification panel (acknowledge / mute)
- Quick actions (start/stop collection, open diagnostics)

**Primary actions:**
- Start/stop collection
- Open diagnostics
- View alerts

---

### 2) Data Management Workspace

**Purpose:** Everything related to symbols, storage, backfill, and ingestion workflows.

**Pages consolidated:**
- SymbolsPage
- SymbolMappingPage
- SymbolStoragePage
- StoragePage
- BackfillPage
- PortfolioImportPage
- IndexSubscriptionPage
- ScheduleManagerPage

**Key features:**
- Symbol browse & filters
- Mapping editor
- Storage status and retention views
- Backfill job creation and history
- Import pipeline for portfolios
- Schedule configuration

**Primary actions:**
- Create backfill job
- Import symbols/portfolio
- Apply retention rule

---

### 3) Monitoring & Health Workspace

**Purpose:** Unified health and quality monitoring for data and services.

**Pages consolidated:**
- DataQualityPage
- CollectionSessionPage
- ArchiveHealthPage
- ServiceManagerPage
- SystemHealthPage
- DiagnosticsPage

**Key features:**
- Health overview (summary + status tiles)
- Data quality rule management
- Session timeline (collection sessions)
- Diagnostics / logs
- Service controls

**Primary actions:**
- Run diagnostics
- Open service control panel
- Review data quality anomalies

---

### 4) Analytics & Visualization Workspace

**Purpose:** Visual analytics, comparisons, and advanced data exploration.

**Pages consolidated:**
- AdvancedAnalyticsPage
- ChartingPage
- OrderBookPage
- DataCalendarPage

**Key features:**
- Chart canvas
- Dockable panels for order book, calendar, indicators
- Saved layouts for analytics views

**Primary actions:**
- Create chart view
- Toggle panels
- Save analytics layout

---

### 5) Tools & Export Workspace

**Purpose:** Consolidated tooling, export workflows, and utility flows.

**Pages consolidated:**
- DataExportPage
- AnalysisExportPage
- AnalysisExportWizardPage
- ExportPresetsPage
- DataSamplingPage
- TimeSeriesAlignmentPage
- EventReplayPage
- PackageManagerPage
- TradingHoursPage

**Key features:**
- Export wizard launcher
- Preset manager
- Data sampling tools
- Alignment and normalization tools
- Replay tooling
- Package manager
- Trading calendar tools

**Primary actions:**
- Run export
- Manage export presets
- Launch replay

---

### 6) Storage & Maintenance Workspace

**Purpose:** Storage optimization, retention, and admin maintenance.

**Pages consolidated:**
- StorageOptimizationPage
- RetentionAssurancePage
- AdminMaintenancePage

**Key features:**
- Optimization planner (compression, migration)
- Retention rules and enforcement views
- Maintenance jobs (manual + scheduled)

**Primary actions:**
- Run optimization
- Execute maintenance job
- Update retention policies

---

### 7) Integrations Workspace

**Purpose:** External integrations and messaging.

**Pages consolidated:**
- LeanIntegrationPage
- MessagingHubPage

**Key features:**
- Integration configuration
- Messaging / webhook routing

**Primary actions:**
- Configure provider integration
- Test messaging route

---

### 8) Settings & Help Workspace

**Purpose:** User preferences, onboarding, and help content.

**Pages consolidated:**
- SettingsPage
- KeyboardShortcutsPage
- HelpPage
- WelcomePage
- SetupWizardPage

**Key features:**
- App configuration
- Keyboard shortcut reference
- Help and onboarding flows

**Primary actions:**
- Update settings
- Launch setup wizard
- View shortcut list

---

## Navigation Wireframe (Text)

```
+-------------------------------------------------------------+
| Top Bar: [Search/Command Palette] [Current Workspace] [User]|
+-------------------------------------------------------------+
| Left Nav (Workspaces)      | Main Workspace Canvas          |
|----------------------------|--------------------------------|
| • Dashboard                | [Workspace Header]             |
| • Data Management          | [Summary cards + tabs]          |
| • Monitoring & Health      | [Tab: Overview | Details]       |
| • Analytics                | [Dockable Panels]               |
| • Tools & Export           | [Wizard/Tool Launcher]          |
| • Storage & Maintenance    | [Planner + Jobs List]           |
| • Integrations             | [Config Panels]                 |
| • Settings & Help          | [Settings + Help content]        |
+----------------------------+--------------------------------+
| Optional: right-side panel (Activity / Notifications / Logs) |
+-------------------------------------------------------------+
```

### Navigation Rules

- **Only 7–8 top-level items** in the left navigation.
- **Workspace tabs** or **pills** for sub-views (rather than deep page navigation).
- **Command palette** (Ctrl+K) to quickly open pages or actions.
- **Activity/Notifications** panel docked to the right on demand.

## Component-Level Recommendations

### 1) Workspace Header
- Title + short description
- Breadcrumb of current sub-view
- Primary action buttons aligned right

### 2) Summary Cards
- Compact cards for quick status
- Use badges for health status (green/yellow/red)

### 3) Tabbed Sub-Views
- Tabbed layout inside each workspace
- Tabs should be keyboard-navigable

### 4) Dockable Panels (Analytics & Monitoring)
- Allow pinned/docked panels for data-heavy views (order book, logs)

### 5) Quick Action Bar
- Provide workflow-specific commands at top of workspace
- Example: “Run Diagnostics”, “Start Backfill”, “Export Data”

### 6) Activity & Notifications Panel
- Slide-out panel for logs and notification feed
- Supports dismiss/acknowledge, filter by type

### 7) Consistent Form Layout
- Use consistent inline validation
- Progressive disclosure for advanced fields

### 8) Dense Data Tables
- Column chooser
- Persisted sort/filter state
- Export to CSV/JSON

## Interaction Patterns

- **Progressive disclosure:** hide advanced configuration by default.
- **Contextual help:** inline tips and tooltips instead of dedicated pages.
- **Global search:** allow search across pages, symbols, jobs, exports.
- **Long-running tasks:** show persistent status chip with quick access to details.

## Information Architecture Summary

| Workspace | Page Count | Consolidated Pages |
|-----------|------------|--------------------|
| Dashboard | 3 | DashboardPage, ActivityLogPage, NotificationCenterPage |
| Data Management | 8 | Symbols, Mapping, Storage, Backfill, Imports, Index Subscription, Schedules |
| Monitoring & Health | 6 | Data Quality, Sessions, Archive Health, Services, System Health, Diagnostics |
| Analytics | 4 | Advanced Analytics, Charting, Order Book, Data Calendar |
| Tools & Export | 9 | Export, Presets, Sampling, Alignment, Replay, Package Manager, Trading Hours |
| Storage & Maintenance | 3 | Optimization, Retention, Admin Maintenance |
| Integrations | 2 | Lean Integration, Messaging Hub |
| Settings & Help | 5 | Settings, Shortcuts, Help, Welcome, Setup Wizard |

## Next Steps

1. Validate consolidated IA with stakeholders.
2. Create low-fidelity mockups for each workspace.
3. Identify shared components to build first (cards, tables, command palette).
4. Map data/telemetry requirements for dashboards and status tiles.
