# Market Data Collector - Standalone UI

A modern React-based dashboard for Market Data Collector, built with Vite, TypeScript, and Tailwind CSS.

## Features

- **Real-time Status Monitoring** - Live metrics, event rates, and integrity alerts
- **Configuration Management** - Update data sources, storage settings, and provider credentials
- **Symbol Management** - Add, edit, and remove symbol subscriptions
- **Historical Backfill** - Run and monitor historical data downloads
- **Help & Documentation** - Built-in guides and command reference

## Prerequisites

- Node.js 18+ (recommended: 20+)
- npm or yarn
- Market Data Collector backend running on port 8080

## Quick Start

```bash
# Install dependencies
npm install

# Start development server
npm run dev
```

The UI will be available at `http://localhost:3000`. It proxies API requests to `http://localhost:8080` automatically.

## Development

```bash
# Start development server with hot reload
npm run dev

# Run linter
npm run lint

# Build for production
npm run build

# Preview production build
npm run preview
```

## Configuration

### API URL

By default, the UI proxies API requests to `http://localhost:8080`. To use a different backend:

**Development:**
Edit `vite.config.ts` proxy target:
```typescript
proxy: {
  '/api': {
    target: 'http://localhost:8080', // Change this
    changeOrigin: true,
  },
}
```

**Production:**
Set the `VITE_API_URL` environment variable:
```bash
VITE_API_URL=https://your-api-server.com/api npm run build
```

## Tech Stack

- **React 18** - UI library
- **TypeScript** - Type safety
- **Vite** - Build tool and dev server
- **Tailwind CSS** - Styling
- **TanStack Query** - Data fetching and caching
- **React Router** - Navigation
- **Lucide React** - Icons
- **React Hot Toast** - Notifications

## Project Structure

```
ui/
├── public/
│   └── favicon.svg
├── src/
│   ├── api/
│   │   └── client.ts        # API client functions
│   ├── components/          # Reusable components
│   ├── hooks/
│   │   └── useApi.ts        # React Query hooks
│   ├── pages/
│   │   ├── StatusPage.tsx   # Real-time monitoring
│   │   ├── ConfigPage.tsx   # Configuration forms
│   │   ├── SymbolsPage.tsx  # Symbol management
│   │   ├── BackfillPage.tsx # Historical backfill
│   │   └── HelpPage.tsx     # Documentation
│   ├── types/
│   │   └── index.ts         # TypeScript types
│   ├── App.tsx              # Main app component
│   ├── main.tsx             # Entry point
│   └── index.css            # Global styles
├── index.html
├── package.json
├── tailwind.config.js
├── tsconfig.json
└── vite.config.ts
```

## Building for Production

```bash
npm run build
```

The build output will be in the `dist/` directory. Serve it with any static file server:

```bash
# Using vite preview
npm run preview

# Using any HTTP server
npx serve dist
```

## Backend Requirements

The standalone UI requires the Market Data Collector backend to be running with CORS enabled (default configuration). The backend serves API endpoints at:

- `GET /api/config` - Load configuration
- `POST /api/config/*` - Update configuration
- `GET /api/status` - System status
- `GET /api/backfill/*` - Backfill operations
- `GET /api/symbols/*` - Symbol management

See the main project documentation for full API reference.

## License

Part of the Market Data Collector project. See the main LICENSE file for details.
