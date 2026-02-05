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
- **Structure**:
  ```
  [Workspace Icon] Workspace Title
  Brief description (1 line)
  Breadcrumb: Home / Workspace / Tab
  
  [Primary Action 1] [Primary Action 2]  [Refresh] [⋮ More]
  ```
- **Behavior**:
  - Sticky header (stays visible on scroll)
  - Primary actions: max 2-3, others in "More" menu
  - Breadcrumb links are clickable for quick navigation
- **Example (Data Management)**:
  ```
  📦 Data Management
  Manage symbols, storage, and data ingestion workflows
  Home / Data Management / Backfill
  
  [New Backfill Job] [Import Symbols]  [Refresh] [⋮]
  ```

### 2) Summary Cards
- **Layout**: Grid of 2-4 cards across (responsive)
- **Structure**:
  ```
  ┌─────────────────────────┐
  │ [Icon] Metric Name      │
  │ 1,234 ▲ +5%            │ ← Large number + trend
  │ Status indicator: ●     │ ← Green/Yellow/Red
  │ Last updated: 2m ago    │
  └─────────────────────────┘
  ```
- **Interactivity**:
  - Clickable → drills into details
  - Hover shows mini tooltip with more context
  - Badge indicator for alerts (red dot with count)
- **Update strategy**: Real-time via WebSocket or 5s polling
- **Examples**:
  - Collection Status: "Active" (green), "Paused" (yellow), "Error" (red)
  - Storage Usage: "78% used" with progress bar
  - Provider Health: "3/4 connected" (yellow if any down)

### 3) Tabbed Sub-Views
- **Style**: Material Design tabs (underline indicator)
- **Structure**:
  ```
  Overview | Details | History | Settings
  ─────────
  [Tab content here]
  ```
- **Behavior**:
  - Keyboard navigation: Alt+1 to Alt+9 for tab shortcuts
  - Remember last selected tab per workspace
  - Lazy-load tab content (render on select)
- **Tab content patterns**:
  - Overview: Summary cards + recent activity
  - Details: Data tables with filters
  - History: Timeline or chronological list
  - Settings: Form with sections

### 4) Dockable Panels (Analytics & Monitoring)
- **Use cases**: Order book, logs, watch list
- **Behavior**:
  - Drag to reposition
  - Resize via drag handles
  - Pin/unpin (collapse to icon bar)
  - Close with X button
  - Save layout per user
- **Implementation (WPF)**:
  - Use AvalonDock or similar docking library
  - Persist layout in user settings JSON
- **Example layout**:
  ```
  ┌────────────────┬──────────────┐
  │ Chart (main)   │ Order Book   │
  │                │  (docked)    │
  ├────────────────┼──────────────┤
  │ Indicators     │ Watch List   │
  └────────────────┴──────────────┘
  ```

### 5) Quick Action Bar
- **Location**: Below workspace header, above content
- **Style**: Horizontal toolbar with icon buttons
- **Content**: 
  - Workflow-specific commands (3-6 actions)
  - Icon + label for clarity
  - Active state indication
- **Examples**:
  - Data Management: [Add Symbol] [Import] [Export] [Refresh]
  - Monitoring: [Run Diagnostics] [View Logs] [Export Report]
  - Analytics: [New Chart] [Save Layout] [Screenshot] [Export Data]

### 6) Activity & Notifications Panel
- **Location**: Right-side slide-out (300-400px wide)
- **Trigger**: 
  - Click notification bell icon (badge shows unread count)
  - Auto-open for critical alerts
- **Structure**:
  ```
  Activity & Notifications [X]
  
  [Filter: All | Alerts | Jobs | System ▼]
  
  ● Critical alert: IB disconnected
    2 minutes ago
    [Reconnect] [Dismiss]
  
  ○ Backfill job completed
    15 minutes ago - 1,234 bars downloaded
    [View Details] [Dismiss]
  
  [Clear all] [Settings]
  ```
- **Features**:
  - Group by time: Today, Yesterday, This Week
  - Filter by type and severity
  - Acknowledge/dismiss actions
  - Persistent (survive page navigation)

### 7) Consistent Form Layout
- **Structure**:
  ```
  Field Label *
  [Input control]
  Help text: Brief explanation
  Error message (if validation fails)
  ```
- **Validation**:
  - Real-time (on blur, not on every keystroke)
  - Inline error messages (red text + icon)
  - Success indicators (green checkmark)
  - Suggested fixes in error messages
- **Progressive disclosure**:
  - Basic fields always visible
  - "Show Advanced Options" expandable section
  - Remember expansion state per form
- **Auto-save**:
  - Draft saved every 30 seconds
  - Restore draft on return
  - "Discard draft" option

### 8) Dense Data Tables
- **Features**:
  - **Column chooser**: Right-click header or gear icon
  - **Sorting**: Click header to sort, visual indicator (▲/▼)
  - **Filtering**: Inline filter row or filter panel
  - **Search**: Global table search box
  - **Pagination**: Virtual scrolling for 1000+ rows
  - **Row actions**: Hover to show action buttons
  - **Multi-select**: Checkbox column for batch operations
  - **Persisted state**: Remember sort, filter, columns per table
- **Export options**: CSV, JSON, Excel (button in table toolbar)
- **Context menu**: Right-click row for quick actions
- **Example**:
  ```
  [Search...] [Columns] [Filter] [Export▼]
  
  ☐ Symbol    Exchange  Last Price  Change   Actions
  ─────────────────────────────────────────────────
  ☐ AAPL      NASDAQ    $175.43    +2.1%    [Edit][Delete]
  ☐ SPY       NYSE      $445.12    -0.3%    [Edit][Delete]
  
  Showing 1-50 of 1,234 [Virtual scroll]
  ```

## Interaction Patterns

### Progressive Disclosure
- **Basic fields** - Always visible: symbol, provider, date range
- **Advanced fields** - Hidden by default, revealed via "Show Advanced" toggle:
  - Rate limiting controls
  - Retry policies
  - Custom endpoints
  - Debug logging options
  - Advanced filters and transformations
- **Pattern**: Use expandable sections with clear labels ("Advanced Options")
- **State preservation**: Remember user's disclosure preferences per workspace

### Contextual Help
- **Inline tips** - Brief (1-2 sentence) help text below complex fields
- **Tooltips** - Hover for definition, Shift+F1 for extended help
- **Validation messages** - Real-time, inline, with suggested fixes
- **No dedicated help pages** - Embed guidance in context where needed

### Global Search (Ctrl+K Command Palette)
- **Scope**: Pages, symbols, jobs, exports, settings, keyboard shortcuts
- **Features**:
  - Fuzzy matching (e.g., "bkfl" → "Backfill")
  - Recent items prioritized
  - Keyboard navigation (arrows, Enter)
  - Type-ahead filtering
- **Actions**: Navigate to page, open job details, jump to symbol

### Long-Running Tasks
- **Persistent status chip** - Fixed position: bottom-right corner
- **Click behavior**: Expand to show progress details (%, ETA, logs)
- **Dismissal**: Allow minimize but keep in background task list
- **Multiple tasks**: Stack vertically, show count badge if > 3
- **States**: Running (blue), Success (green), Warning (yellow), Error (red)

## State Management

### Navigation State
- **History stack** - Store last 10 navigations for Back button
- **Workspace context** - Remember selected tab per workspace
- **Deep linking** - Support URL-style navigation (e.g., `/workspace/data-management/tab/backfill`)
- **Session restoration** - On app restart, restore last workspace and tab

### Form State
- **Auto-save** - Draft state saved every 30 seconds for complex forms
- **Unsaved changes warning** - Prompt before navigation if form is dirty
- **Context preservation** - If editing a symbol and switching workspaces, preserve draft
- **Validation state** - Cache validation results to avoid re-validation on navigation

### Filter & Search State
- **Per-workspace persistence** - Remember filters, sort order, search terms
- **Clear filters action** - Quick reset to default state
- **Filter presets** - Save common filter combinations for reuse

### Background Tasks State
- **Task queue** - FIFO queue for long-running operations
- **Progress tracking** - Persist task state (e.g., backfill progress) across app restarts
- **Task history** - Last 50 completed tasks with results and logs

## Error Handling & States

### Error Display Strategy
- **Inline errors** - Form validation errors appear below field
- **Notification errors** - API failures show as toast notifications (5-second auto-dismiss)
- **Critical errors** - Modal dialog for errors requiring user action
- **Error panel** - Collapsible right-side panel for error log (filterable by severity)

### Empty States
- **Dashboard** - "No active collection" with "Start Collection" CTA
- **Symbol list** - "No symbols configured" with "Add Symbols" CTA
- **Backfill history** - "No backfill jobs run" with "Create Backfill Job" CTA
- **Analytics charts** - "No data available" with date range adjuster
- **Activity feed** - "No recent activity" with refresh button

### Loading States
- **Initial page load** - Skeleton UI with shimmer effect (200ms delay before showing)
- **Data refresh** - Subtle spinner in header, don't block UI
- **Long operations** - Progress bar with ETA and cancel button
- **Infinite scroll** - Spinner at bottom of list, load next page

### Error Recovery
- **Retry action** - Auto-retry with exponential backoff (3 attempts)
- **Manual retry** - "Retry" button in error message
- **Offline mode** - Queue operations for later, show offline indicator
- **Partial success** - Show which items succeeded/failed in batch operations

## Accessibility Requirements

### WCAG 2.1 Level AA Compliance
- **Keyboard navigation** - All features accessible via keyboard
- **Focus management** - Visible focus indicator, logical tab order
- **Screen reader support** - ARIA labels, roles, live regions for dynamic content
- **Color contrast** - Minimum 4.5:1 for text, 3:1 for UI components
- **Text resizing** - Support up to 200% zoom without horizontal scrolling

### Keyboard Navigation
- **Tab order** - Left nav → main content → right panel → footer
- **Skip links** - "Skip to main content" at top of page
- **Focus trapping** - In modals and dialogs, focus stays within dialog
- **Escape key** - Close modals, cancel operations, clear search

### Screen Reader Features
- **Landmarks** - Proper use of `<nav>`, `<main>`, `<aside>`, `<footer>`
- **Headings** - Logical heading hierarchy (H1 → H2 → H3)
- **Live regions** - Announce status changes, notifications, errors
- **Alt text** - Descriptive alt text for all images and icons

### Keyboard Shortcuts
| Shortcut | Action |
|----------|--------|
| `Ctrl+K` | Open command palette |
| `Ctrl+/` | Show keyboard shortcuts help |
| `Ctrl+1` to `Ctrl+8` | Navigate to workspace 1-8 |
| `Alt+Left` / `Alt+Right` | Navigate back/forward |
| `Ctrl+Shift+T` | Toggle light/dark theme |
| `Ctrl+Shift+F` | Focus global search |
| `F5` | Refresh current view |
| `Escape` | Cancel operation, close modal |

## Performance Targets

### Load Time Requirements
- **Initial page load** - < 1.5 seconds to interactive
- **Workspace switch** - < 300ms transition
- **Data refresh** - < 500ms for cached data, < 2s for API calls
- **Search/filter** - < 100ms response time (debounced 300ms)
- **Backfill job start** - < 1s to show confirmation

### Rendering Performance
- **Table virtualization** - Render only visible rows (100+ rows)
- **Chart rendering** - < 1s to render 10,000 data points
- **Memory usage** - < 500MB for typical workload
- **Scroll performance** - 60 FPS for smooth scrolling

### Data Refresh Strategy
- **Real-time data** - WebSocket for live ticker updates (< 100ms latency)
- **Dashboard metrics** - HTTP polling every 5 seconds
- **Activity feed** - HTTP polling every 10 seconds
- **Historical data** - On-demand load, cached for 5 minutes
- **Background sync** - Queue low-priority updates for idle time

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

## User Experience Patterns

### Discoverability Strategy
- **First-run onboarding** - Guided tour highlighting key workspaces (dismissible)
- **Workspace tooltips** - Hover on nav item shows brief description
- **Contextual prompts** - Suggest next actions based on current state
  - Example: "No symbols configured. Would you like to add some?"
- **Search-driven discovery** - Command palette (Ctrl+K) surfaces all features
- **Help shortcuts** - F1 key context-sensitive help for current page

### Context Preservation
- **Workspace memory** - Each workspace remembers:
  - Selected tab
  - Scroll position
  - Filter state
  - Selected items
- **Cross-workspace editing** - If editing a symbol in Data Management:
  - Switch to Monitoring → state preserved
  - Return to Data Management → resume editing same symbol
- **Draft recovery** - Unsaved form changes persist until explicitly discarded
- **Navigation breadcrumbs** - Always show current location path

### Batch Operations
- **Multi-select pattern**:
  - Checkbox column in tables
  - Shift+Click for range selection
  - Ctrl+A to select all (with confirmation for > 100 items)
- **Bulk action bar** - Appears above table when items selected:
  - "X items selected" with deselect all option
  - Available actions: Delete, Export, Tag, Archive
- **Progress for batch operations**:
  - Show modal with progress bar and item-by-item status
  - Allow cancel (partial completion)
  - Show summary on completion (5 succeeded, 2 failed)

### Conflict Resolution
- **Long-running jobs** - User can navigate away, job continues:
  - Persistent status chip in bottom-right tracks progress
  - Notification on completion/failure
  - Job history shows all runs
- **Concurrent editing** - Prevent conflicts:
  - Lock indicator if item being edited elsewhere
  - Auto-refresh if data changed externally
  - Merge prompt if conflicts detected
- **Unsaved changes** - Before navigation:
  - Modal: "You have unsaved changes. Save, Discard, or Cancel?"
  - Auto-save draft option (save and navigate)

## Task Flow Diagrams

### Flow 1: Create Backfill Job
```
Dashboard → Data Management workspace
         → Select "Backfill" tab
         → Click "New Backfill Job"
         → Wizard dialog opens:
            Step 1: Select provider (dropdown with capabilities)
            Step 2: Choose symbols (multi-select with search)
            Step 3: Set date range (calendar picker)
            Step 4: Review & schedule (preview data count estimate)
         → Click "Start Backfill"
         → Job queued, status chip appears
         → Navigate to "Backfill History" tab to monitor
         → Notification on completion
```

### Flow 2: Monitor Data Quality
```
Dashboard → See quality alert badge on Health workspace
         → Click "Monitoring & Health"
         → Lands on "Overview" tab (default)
         → See quality summary cards:
            - Completeness: 98% (green)
            - Gaps detected: 3 (yellow)
            - Anomalies: 0 (green)
         → Click "3 Gaps" card
         → Navigate to "Data Quality" tab
         → Gap table shows: Symbol, Date Range, Duration
         → Select gap, click "Fill Gap"
         → Backfill wizard pre-filled with gap details
         → Confirm and start gap fill
```

### Flow 3: Export Data for Analysis
```
Analytics workspace → "Charting" tab
                   → Select symbol, date range from controls
                   → Chart renders with data
                   → Click "Export" button in toolbar
                   → Export dialog opens:
                      - Format: CSV, JSON, Parquet, Excel
                      - Fields: Select columns (checkboxes)
                      - Time range: Use chart range or custom
                   → Click "Export"
                   → File download starts
                   → Success notification with file path
                   → Option: "Open in Analysis Tool"
```

### Flow 4: Configure New Symbol
```
Data Management workspace → "Symbols" tab
                         → Click "Add Symbol" button
                         → Dialog opens:
                            - Symbol (text input with validation)
                            - Exchange (dropdown)
                            - Data types (checkboxes: Trades, Quotes, L2)
                            - Providers (multi-select with fallback order)
                         → Click "Add"
                         → Symbol added to table
                         → Auto-start collection if "Start immediately" checked
                         → Status indicator shows: Connecting → Active
```

### Flow 5: Review System Health
```
Dashboard → Glance at status summary cards:
           - Collection Status: Active (green)
           - Providers: 3/4 connected (yellow)
           - Storage: 78% used (green)
         → Click "Providers" card with yellow indicator
         → Navigate to Monitoring & Health → "Provider Health" tab
         → Table shows:
           - Alpaca: Connected (latency: 45ms)
           - Polygon: Connected (latency: 120ms)
           - IB: Disconnected (error: timeout)
         → Click "IB" row → Details panel opens
         → See error log and retry history
         → Click "Reconnect" button
         → Status updates to "Connecting..." → "Connected"
```

### Flow 6: First-Time Setup
```
App Launch (first run) → Setup Wizard dialog opens:
                       → Welcome screen (skip option)
                       → Step 1: Configure storage path
                       → Step 2: Add API credentials (Alpaca, Polygon, etc.)
                       → Step 3: Select initial symbols (search + popular list)
                       → Step 4: Choose collection schedule (24/7, market hours, custom)
                       → Review summary
                       → Click "Start Collecting"
                       → Wizard closes, lands on Dashboard
                       → Collection starts, activity feed updates
```

## Migration & Transition Plan

### Rollout Strategy
- **Phase 1: Opt-in Beta** (2 weeks)
  - Add "Try New UI" toggle in Settings
  - Beta badge in new UI header
  - Feedback link prominently displayed
  - Collect telemetry on feature usage
  
- **Phase 2: Default with Opt-out** (2 weeks)
  - New UI becomes default for new users
  - Existing users see migration prompt on launch
  - "Switch to Classic UI" option in Settings
  - Monitor support requests and issues
  
- **Phase 3: Full Migration** (1 week)
  - Remove classic UI option
  - Archive old code
  - Final documentation update

### User Communication
- **In-app announcement** - Banner on first launch: "Welcome to the redesigned UI!"
- **Migration guide** - Link to documentation in Help workspace
- **Video walkthrough** - 2-minute overview of new navigation
- **Changelog** - Detailed list of changes and improvements

### Backward Compatibility
- **Keyboard shortcuts** - Preserve existing shortcuts where possible
- **URL parameters** - Redirect old deep links to new workspace structure
- **Export formats** - No breaking changes to data exports
- **Configuration files** - Auto-migrate old settings to new schema

## Visual Design System

### Color Palette
**Light Theme**:
- Primary: Blue (#2196F3)
- Success: Green (#4CAF50)
- Warning: Amber (#FFC107)
- Error: Red (#F44336)
- Background: White (#FFFFFF)
- Surface: Gray (#F5F5F5)
- Text: Dark Gray (#212121)

**Dark Theme**:
- Primary: Light Blue (#64B5F6)
- Success: Green (#81C784)
- Warning: Amber (#FFD54F)
- Error: Red (#E57373)
- Background: Dark Gray (#121212)
- Surface: Charcoal (#1E1E1E)
- Text: White (#FFFFFF)

### Typography
- **Headings**: Segoe UI (Windows), SF Pro (macOS)
  - H1: 32px, Bold
  - H2: 24px, Semibold
  - H3: 18px, Medium
  - H4: 16px, Medium
- **Body**: 14px, Regular
- **Small**: 12px, Regular
- **Code/Data**: Consolas, Monospace, 13px

### Spacing System
- **Base unit**: 8px
- **Margins**:
  - xs: 4px
  - sm: 8px
  - md: 16px
  - lg: 24px
  - xl: 32px
- **Component padding**: 16px (forms), 8px (cards), 4px (buttons)

### Icon System
- **Library**: Material Design Icons or Segoe MDL2 Assets (Windows)
- **Sizes**: 16px (inline), 24px (buttons), 32px (headers), 48px (empty states)
- **Style**: Outlined for secondary actions, filled for primary

### Elevation & Shadows
- **Card**: 2dp elevation (subtle shadow)
- **Modal**: 8dp elevation
- **Dropdown**: 4dp elevation
- **Hover**: +2dp (lift effect)

## Implementation Priorities

### Phase 1: Foundation (Weeks 1-2)
1. **Navigation shell** - Left nav with 8 workspace items
2. **Command palette** (Ctrl+K) - Global search/navigation
3. **Theme system** - Light/dark theme switching
4. **Base layouts** - Workspace header, tab system, content areas
5. **Shared components** - Cards, buttons, form controls

### Phase 2: Core Workspaces (Weeks 3-6)
1. **Dashboard** - Status cards, activity feed
2. **Data Management** - Symbol management, backfill UI
3. **Monitoring & Health** - Quality metrics, provider status

### Phase 3: Advanced Features (Weeks 7-10)
1. **Analytics** - Charting, dockable panels
2. **Tools & Export** - Export wizards, presets
3. **Storage & Maintenance** - Optimization planner

### Phase 4: Polish & Testing (Weeks 11-12)
1. **Accessibility audit** - WCAG 2.1 AA compliance
2. **Performance optimization** - Load time, rendering
3. **User testing** - Gather feedback, iterate
4. **Documentation** - User guide, keyboard shortcuts

## Success Metrics

### Quantitative Metrics
- **Task completion time** - 30% reduction in common workflows
- **Navigation depth** - Average 2 clicks to any feature (vs 4 in old UI)
- **Search usage** - 40% of users adopt command palette
- **Error rate** - 50% reduction in form validation errors
- **Page load time** - < 1.5s initial load, < 300ms workspace switch

### Qualitative Metrics
- **User satisfaction** - SUS (System Usability Scale) score > 80
- **Discoverability** - 80% of users find new features without help
- **Aesthetic appeal** - 90% positive feedback on visual design
- **Accessibility** - 100% WCAG 2.1 AA compliance

### Adoption Metrics
- **Beta participation** - 50+ users in Phase 1
- **Feedback volume** - 100+ feedback items collected
- **Rollout success** - < 5% users revert to old UI
- **Support tickets** - 40% reduction in UI-related tickets

## Next Steps

1. Validate consolidated IA with stakeholders.
2. Create low-fidelity mockups for each workspace.
3. Identify shared components to build first (cards, tables, command palette).
4. Map data/telemetry requirements for dashboards and status tiles.
