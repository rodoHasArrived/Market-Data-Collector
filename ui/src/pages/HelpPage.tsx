import {
  BookOpen,
  Terminal,
  Settings,
  Database,
  Clock,
  Activity,
  ExternalLink,
  ChevronRight,
} from 'lucide-react';

export default function HelpPage() {
  return (
    <div className="space-y-6 animate-fadeIn">
      {/* Header */}
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Help & Documentation</h2>
        <p className="text-sm text-gray-500 mt-1">
          Learn how to use Market Data Collector effectively
        </p>
      </div>

      {/* Quick Start */}
      <div className="card">
        <div className="card-header flex items-center space-x-2">
          <BookOpen className="w-5 h-5 text-primary-600" />
          <h3 className="text-lg font-semibold text-gray-900">Quick Start Guide</h3>
        </div>
        <div className="card-body">
          <ol className="space-y-4">
            <Step
              number={1}
              title="Configure Data Source"
              description="Select your market data provider (Interactive Brokers, Alpaca, or Polygon) and enter your API credentials."
            />
            <Step
              number={2}
              title="Add Symbols"
              description="Add the symbols you want to collect data for. Configure trade and/or depth subscriptions for each symbol."
            />
            <Step
              number={3}
              title="Configure Storage"
              description="Set up your preferred file naming convention, date partitioning, and retention policies."
            />
            <Step
              number={4}
              title="Start Collection"
              description="Launch the collector with your configuration. Data will be written to JSONL files under the data root."
            />
          </ol>
        </div>
      </div>

      {/* Feature Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <FeatureCard
          icon={<Activity className="w-6 h-6" />}
          title="Real-Time Monitoring"
          description="View live metrics including events per second, drop rates, and integrity events. The status page auto-refreshes every 2 seconds."
          tips={[
            'Check drop rate regularly - high values indicate backpressure',
            'Integrity events show data quality issues',
            'Use Prometheus metrics for production monitoring',
          ]}
        />

        <FeatureCard
          icon={<Settings className="w-6 h-6" />}
          title="Configuration"
          description="All settings are stored in appsettings.json and can be modified via the UI or directly in the file."
          tips={[
            'Use environment variables for sensitive credentials',
            'Hot reload is enabled - changes apply without restart',
            'Check the sample config for all available options',
          ]}
        />

        <FeatureCard
          icon={<Database className="w-6 h-6" />}
          title="Symbol Management"
          description="Manage your symbol subscriptions with support for stocks, futures, options, and forex."
          tips={[
            'Use LocalSymbol for futures and preferred shares',
            'SMART exchange routes to best available venue',
            'Depth levels determine order book depth (1-20)',
          ]}
        />

        <FeatureCard
          icon={<Clock className="w-6 h-6" />}
          title="Historical Backfill"
          description="Download historical daily bars from free data providers with automatic failover."
          tips={[
            'Composite provider tries multiple sources automatically',
            'Yahoo Finance has the broadest coverage',
            'Adjusted prices account for splits and dividends',
          ]}
        />
      </div>

      {/* Command Line Reference */}
      <div className="card">
        <div className="card-header flex items-center space-x-2">
          <Terminal className="w-5 h-5 text-primary-600" />
          <h3 className="text-lg font-semibold text-gray-900">Command Line Reference</h3>
        </div>
        <div className="card-body">
          <div className="space-y-4">
            <CommandExample
              command="./MarketDataCollector --ui"
              description="Start with web dashboard on port 8080"
            />
            <CommandExample
              command="./MarketDataCollector --serve-status --watch-config"
              description="Production mode with status endpoint and hot-reload"
            />
            <CommandExample
              command="./MarketDataCollector --selftest"
              description="Run self-diagnostic tests"
            />
            <CommandExample
              command="./MarketDataCollector --backfill --backfill-provider stooq --backfill-symbols SPY,QQQ"
              description="Run historical backfill for specific symbols"
            />
            <CommandExample
              command="./MarketDataCollector --http-port 9090"
              description="Run with custom HTTP port"
            />
          </div>
        </div>
      </div>

      {/* External Links */}
      <div className="card">
        <div className="card-header">
          <h3 className="text-lg font-semibold text-gray-900">Documentation & Resources</h3>
        </div>
        <div className="card-body">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <DocLink
              title="Getting Started Guide"
              description="Complete setup instructions for local development"
              href="https://github.com/rodoHasArrived/Test/blob/main/MarketDataCollector/docs/GETTING_STARTED.md"
            />
            <DocLink
              title="Configuration Reference"
              description="Detailed explanation of all settings"
              href="https://github.com/rodoHasArrived/Test/blob/main/MarketDataCollector/docs/CONFIGURATION.md"
            />
            <DocLink
              title="Architecture Overview"
              description="System design and component diagrams"
              href="https://github.com/rodoHasArrived/Test/blob/main/MarketDataCollector/docs/architecture.md"
            />
            <DocLink
              title="Operator Runbook"
              description="Production deployment and operations guide"
              href="https://github.com/rodoHasArrived/Test/blob/main/MarketDataCollector/docs/operator-runbook.md"
            />
            <DocLink
              title="Lean Integration"
              description="QuantConnect Lean Engine integration guide"
              href="https://github.com/rodoHasArrived/Test/blob/main/MarketDataCollector/docs/lean-integration.md"
            />
            <DocLink
              title="Interactive Brokers Setup"
              description="IB TWS/Gateway configuration"
              href="https://github.com/rodoHasArrived/Test/blob/main/MarketDataCollector/docs/interactive-brokers-setup.md"
            />
          </div>
        </div>
      </div>

      {/* Version Info */}
      <div className="text-center text-sm text-gray-500">
        <p>Market Data Collector v1.0.0</p>
        <p className="mt-1">
          <a
            href="https://github.com/rodoHasArrived/Test"
            target="_blank"
            rel="noopener noreferrer"
            className="text-primary-600 hover:text-primary-700"
          >
            View on GitHub
          </a>
        </p>
      </div>
    </div>
  );
}

interface StepProps {
  number: number;
  title: string;
  description: string;
}

function Step({ number, title, description }: StepProps) {
  return (
    <li className="flex items-start space-x-4">
      <div className="flex-shrink-0 w-8 h-8 bg-gradient-primary rounded-full flex items-center justify-center text-white font-bold text-sm">
        {number}
      </div>
      <div>
        <p className="font-medium text-gray-900">{title}</p>
        <p className="text-sm text-gray-500">{description}</p>
      </div>
    </li>
  );
}

interface FeatureCardProps {
  icon: React.ReactNode;
  title: string;
  description: string;
  tips: string[];
}

function FeatureCard({ icon, title, description, tips }: FeatureCardProps) {
  return (
    <div className="card">
      <div className="card-header flex items-center space-x-3">
        <div className="p-2 bg-primary-50 rounded-lg text-primary-600">{icon}</div>
        <h3 className="text-lg font-semibold text-gray-900">{title}</h3>
      </div>
      <div className="card-body">
        <p className="text-sm text-gray-600 mb-4">{description}</p>
        <div className="space-y-2">
          {tips.map((tip, idx) => (
            <div key={idx} className="flex items-start space-x-2 text-sm">
              <ChevronRight className="w-4 h-4 text-primary-500 flex-shrink-0 mt-0.5" />
              <span className="text-gray-600">{tip}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

interface CommandExampleProps {
  command: string;
  description: string;
}

function CommandExample({ command, description }: CommandExampleProps) {
  return (
    <div className="p-3 bg-gray-900 rounded-lg">
      <code className="text-sm text-emerald-400 font-mono">{command}</code>
      <p className="text-xs text-gray-400 mt-1">{description}</p>
    </div>
  );
}

interface DocLinkProps {
  title: string;
  description: string;
  href: string;
}

function DocLink({ title, description, href }: DocLinkProps) {
  return (
    <a
      href={href}
      target="_blank"
      rel="noopener noreferrer"
      className="flex items-center justify-between p-4 bg-gray-50 hover:bg-gray-100 rounded-lg transition-colors group"
    >
      <div>
        <p className="font-medium text-gray-900 group-hover:text-primary-600">{title}</p>
        <p className="text-xs text-gray-500">{description}</p>
      </div>
      <ExternalLink className="w-4 h-4 text-gray-400 group-hover:text-primary-600" />
    </a>
  );
}
