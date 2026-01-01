import { Routes, Route, NavLink, Navigate } from 'react-router-dom';
import { Activity, Settings, Database, Clock, HelpCircle } from 'lucide-react';
import StatusPage from './pages/StatusPage';
import ConfigPage from './pages/ConfigPage';
import SymbolsPage from './pages/SymbolsPage';
import BackfillPage from './pages/BackfillPage';
import HelpPage from './pages/HelpPage';

function App() {
  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-gradient-primary text-white shadow-lg">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            <div className="flex items-center space-x-3">
              <div className="w-10 h-10 bg-white/20 rounded-lg flex items-center justify-center">
                <Activity className="w-6 h-6" />
              </div>
              <div>
                <h1 className="text-xl font-bold">Market Data Collector</h1>
                <p className="text-xs text-white/70">v1.0.0</p>
              </div>
            </div>
            <nav className="flex items-center space-x-1">
              <NavItem to="/status" icon={<Activity className="w-4 h-4" />} label="Status" />
              <NavItem to="/config" icon={<Settings className="w-4 h-4" />} label="Config" />
              <NavItem to="/symbols" icon={<Database className="w-4 h-4" />} label="Symbols" />
              <NavItem to="/backfill" icon={<Clock className="w-4 h-4" />} label="Backfill" />
              <NavItem to="/help" icon={<HelpCircle className="w-4 h-4" />} label="Help" />
            </nav>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <Routes>
          <Route path="/" element={<Navigate to="/status" replace />} />
          <Route path="/status" element={<StatusPage />} />
          <Route path="/config" element={<ConfigPage />} />
          <Route path="/symbols" element={<SymbolsPage />} />
          <Route path="/backfill" element={<BackfillPage />} />
          <Route path="/help" element={<HelpPage />} />
        </Routes>
      </main>

      {/* Footer */}
      <footer className="bg-white border-t border-gray-200 mt-auto">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <p className="text-center text-sm text-gray-500">
            Market Data Collector &copy; {new Date().getFullYear()} &middot;
            <a href="https://github.com/rodoHasArrived/Test" className="ml-1 text-primary-600 hover:text-primary-700">
              GitHub
            </a>
          </p>
        </div>
      </footer>
    </div>
  );
}

interface NavItemProps {
  to: string;
  icon: React.ReactNode;
  label: string;
}

function NavItem({ to, icon, label }: NavItemProps) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        `flex items-center space-x-2 px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
          isActive
            ? 'bg-white/20 text-white'
            : 'text-white/80 hover:bg-white/10 hover:text-white'
        }`
      }
    >
      {icon}
      <span>{label}</span>
    </NavLink>
  );
}

export default App;
