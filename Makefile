# =============================================================================
# Market Data Collector - Makefile
# =============================================================================
#
# Common development and deployment tasks
#
# Usage:
#   make help           Show available commands
#   make install        Interactive installation
#   make docker         Build and start Docker container
#   make run            Run the application locally
#   make test           Run tests
#
# =============================================================================

.PHONY: help install docker docker-build docker-up docker-down docker-logs \
        run run-ui run-backfill test build publish clean check-deps \
        setup-config lint benchmark docs verify-adrs verify-contracts gen-context \
        gen-interfaces gen-structure gen-providers gen-workflows update-claude-md docs-all \
        doctor doctor-quick doctor-fix diagnose diagnose-build \
        collect-debug collect-debug-minimal build-profile build-binlog validate-data analyze-errors \
        build-graph fingerprint env-capture env-diff impact bisect metrics history app-metrics \
        icons desktop desktop-publish install-hooks \
        build-wpf build-uwp test-desktop-services desktop-dev-bootstrap uwp-xaml-diagnose \
        ai-audit ai-audit-code ai-audit-docs ai-audit-tests ai-verify ai-report

# Default target
.DEFAULT_GOAL := help

# Project settings
PROJECT := src/MarketDataCollector/MarketDataCollector.csproj
UI_PROJECT := src/MarketDataCollector.Ui/MarketDataCollector.Ui.csproj
DESKTOP_PROJECT := src/MarketDataCollector.Uwp/MarketDataCollector.Uwp.csproj
WPF_PROJECT := src/MarketDataCollector.Wpf/MarketDataCollector.Wpf.csproj
TEST_PROJECT := tests/MarketDataCollector.Tests/MarketDataCollector.Tests.csproj
BENCHMARK_PROJECT := benchmarks/MarketDataCollector.Benchmarks/MarketDataCollector.Benchmarks.csproj
DOCGEN_PROJECT := build/dotnet/DocGenerator/DocGenerator.csproj
DOCKER_IMAGE := marketdatacollector:latest
HTTP_PORT ?= 8080
BUILDCTL := python3 build/python/cli/buildctl.py
BUILD_VERBOSITY ?= normal
APPINSTALLER_URI ?=
SIGNING_CERT_PFX ?=
SIGNING_CERT_PASSWORD ?=

ifeq ($(V),0)
	BUILD_VERBOSITY := quiet
endif
ifeq ($(V),2)
	BUILD_VERBOSITY := verbose
endif
ifeq ($(V),3)
	BUILD_VERBOSITY := debug
endif

MSIX_APPINSTALLER_FLAGS :=
MSIX_SIGNING_FLAGS :=
ifneq ($(strip $(APPINSTALLER_URI)),)
	MSIX_APPINSTALLER_FLAGS := -p:GenerateAppInstallerFile=true -p:AppInstallerUri=$(APPINSTALLER_URI) -p:AppInstallerCheckForUpdateFrequency=OnApplicationRun -p:AppInstallerUpdateFrequency=1
endif
ifneq ($(strip $(SIGNING_CERT_PFX)),)
	MSIX_SIGNING_FLAGS := -p:PackageCertificateKeyFile=$(SIGNING_CERT_PFX) -p:PackageCertificatePassword=$(SIGNING_CERT_PASSWORD)
else
	MSIX_SIGNING_FLAGS := -p:GenerateTemporaryStoreCertificate=true
endif

# Colors
GREEN := \033[0;32m
YELLOW := \033[1;33m
BLUE := \033[0;34m
NC := \033[0m # No Color

# =============================================================================
# Help
# =============================================================================

help: ## Show this help message
	@echo ""
	@echo "╔══════════════════════════════════════════════════════════════════════╗"
	@echo "║              Market Data Collector - Make Commands                   ║"
	@echo "╚══════════════════════════════════════════════════════════════════════╝"
	@echo ""
	@echo "$(BLUE)Installation:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | grep -E 'install|setup' | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-18s$(NC) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BLUE)Docker:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | grep -E 'docker' | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-18s$(NC) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BLUE)Development:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | grep -E 'run|build|test|clean|bench|lint' | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-18s$(NC) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BLUE)Documentation:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | grep -E 'docs|verify-adr|verify-contract|gen-context|gen-interface|gen-structure|gen-provider|gen-workflow|update-claude' | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-18s$(NC) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BLUE)Publishing:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | grep -E 'publish' | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-18s$(NC) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BLUE)Desktop App:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | grep -E 'icons|desktop' | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-18s$(NC) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BLUE)Diagnostics:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | grep -E 'doctor|diagnose|collect-debug|build-profile|build-binlog|build-graph|fingerprint|env-|impact|bisect|metrics|history|validate-data|analyze-errors' | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-18s$(NC) %s\n", $$1, $$2}'
	@echo ""

# =============================================================================
# Installation
# =============================================================================

install: ## Interactive installation (Docker or Native)
	@./build/scripts/install/install.sh

install-docker: ## Docker-based installation
	@./build/scripts/install/install.sh --docker

install-native: ## Native .NET installation
	@./build/scripts/install/install.sh --native

setup-config: ## Create appsettings.json from template
	@if [ ! -f config/appsettings.json ]; then \
		cp config/appsettings.sample.json config/appsettings.json; \
		echo "$(GREEN)Created config/appsettings.json$(NC)"; \
		echo "$(YELLOW)Remember to edit with your API credentials$(NC)"; \
	else \
		echo "$(YELLOW)config/appsettings.json already exists$(NC)"; \
	fi
	@mkdir -p data logs

check-deps: ## Check prerequisites
	@./build/scripts/install/install.sh --check

# =============================================================================
# Docker
# =============================================================================

docker: ## Build and start Docker container
	@./build/scripts/install/install.sh --docker

docker-build: ## Build Docker image
	@echo "$(BLUE)Building Docker image...$(NC)"
	docker build -f deploy/docker/Dockerfile -t $(DOCKER_IMAGE) .

docker-up: setup-config ## Start Docker container
	@echo "$(BLUE)Starting Docker container...$(NC)"
	docker compose -f deploy/docker/docker-compose.yml up -d
	@echo "$(GREEN)Container started!$(NC)"
	@echo "  Dashboard: http://localhost:$(HTTP_PORT)"
	@echo "  Health:    http://localhost:$(HTTP_PORT)/health"
	@echo "  Metrics:   http://localhost:$(HTTP_PORT)/metrics"

docker-down: ## Stop Docker container
	docker compose -f deploy/docker/docker-compose.yml down

docker-logs: ## View Docker logs
	docker compose -f deploy/docker/docker-compose.yml logs -f

docker-restart: ## Restart Docker container
	docker compose -f deploy/docker/docker-compose.yml restart

docker-clean: ## Remove Docker containers and images
	docker compose -f deploy/docker/docker-compose.yml down -v
	docker rmi $(DOCKER_IMAGE) 2>/dev/null || true

docker-monitoring: ## Start with Prometheus and Grafana
	docker compose -f deploy/docker/docker-compose.yml --profile monitoring up -d
	@echo "$(GREEN)Monitoring stack started!$(NC)"
	@echo "  Prometheus: http://localhost:9090"
	@echo "  Grafana:    http://localhost:3000 (admin/admin)"

# =============================================================================
# Development
# =============================================================================

build: ## Build the project
	@echo "$(BLUE)Building with observability...$(NC)"
	@BUILD_VERBOSITY=$(BUILD_VERBOSITY) $(BUILDCTL) build --project $(PROJECT) --configuration Release

run: setup-config ## Run the collector
	@echo "$(BLUE)Running collector...$(NC)"
	dotnet run --project $(PROJECT) -- --http-port $(HTTP_PORT) --watch-config

run-ui: setup-config ## Run with web dashboard
	@echo "$(BLUE)Starting web dashboard on port $(HTTP_PORT)...$(NC)"
	dotnet run --project $(PROJECT) -- --ui --http-port $(HTTP_PORT)

run-backfill: setup-config ## Run historical backfill
	@echo "$(BLUE)Running backfill...$(NC)"
	@if [ -z "$(SYMBOLS)" ]; then \
		dotnet run --project $(PROJECT) -- --backfill; \
	else \
		dotnet run --project $(PROJECT) -- --backfill --backfill-symbols $(SYMBOLS); \
	fi

run-selftest: ## Run self-tests
	dotnet run --project $(PROJECT) -- --selftest

test: ## Run unit tests
	@echo "$(BLUE)Running tests...$(NC)"
	dotnet test $(TEST_PROJECT) --logger "console;verbosity=normal"

test-coverage: ## Run tests with coverage
	dotnet test $(TEST_PROJECT) --collect:"XPlat Code Coverage"

benchmark: ## Run benchmarks
	@echo "$(BLUE)Running benchmarks...$(NC)"
	dotnet run --project $(BENCHMARK_PROJECT) -c Release

lint: ## Check code formatting
	dotnet format $(PROJECT) --verify-no-changes

format: ## Format code
	dotnet format $(PROJECT)

install-hooks: ## Install git pre-commit hooks (enforces dotnet format)
	@./build/scripts/hooks/install-hooks.sh

clean: ## Clean build artifacts
	@echo "$(BLUE)Cleaning...$(NC)"
	dotnet clean
	rm -rf bin/ obj/ publish/

# =============================================================================
# Publishing
# =============================================================================

publish: ## Publish for all platforms
	@echo "$(BLUE)Publishing for all platforms...$(NC)"
	./build/scripts/publish/publish.sh

publish-linux: ## Publish for Linux x64
	./build/scripts/publish/publish.sh linux-x64

publish-windows: ## Publish for Windows x64
	./build/scripts/publish/publish.sh win-x64

publish-macos: ## Publish for macOS x64
	./build/scripts/publish/publish.sh osx-x64

# =============================================================================
# Utilities
# =============================================================================

health: ## Check application health
	@curl -s http://localhost:$(HTTP_PORT)/health | jq . 2>/dev/null || echo "Application not running or jq not installed"

status: ## Get application status
	@curl -s http://localhost:$(HTTP_PORT)/status | jq . 2>/dev/null || echo "Application not running or jq not installed"

app-metrics: ## Get Prometheus metrics from running app
	@curl -s http://localhost:$(HTTP_PORT)/metrics

version: ## Show version information
	@echo "Market Data Collector v1.1.0"
	@dotnet --version 2>/dev/null && echo ".NET SDK: $$(dotnet --version)" || echo ".NET SDK: Not installed"
	@docker --version 2>/dev/null || echo "Docker: Not installed"

# =============================================================================
# Documentation
# =============================================================================

docs: gen-context verify-adrs ## Generate all documentation from code
	@echo "$(GREEN)Documentation generated and verified$(NC)"

gen-context: ## Generate project-context.md from code annotations
	@echo "$(BLUE)Generating project context from code...$(NC)"
	@dotnet build $(DOCGEN_PROJECT) -c Release -v q
	@dotnet run --project $(DOCGEN_PROJECT) --no-build -c Release -- context \
		--src src/MarketDataCollector \
		--output docs/generated/project-context.md \
		--xml-docs src/MarketDataCollector/bin/Release/net9.0/MarketDataCollector.xml
	@echo "$(GREEN)Generated docs/generated/project-context.md$(NC)"

verify-adrs: ## Verify ADR implementation links are valid
	@echo "$(BLUE)Verifying ADR implementation links...$(NC)"
	@dotnet build $(DOCGEN_PROJECT) -c Release -v q
	@dotnet run --project $(DOCGEN_PROJECT) --no-build -c Release -- verify-adrs \
		--adr-dir docs/adr \
		--src-dir .
	@echo "$(GREEN)ADR verification complete$(NC)"

verify-contracts: build ## Verify runtime contracts at startup
	@echo "$(BLUE)Verifying contracts...$(NC)"
	dotnet run --project $(PROJECT) --no-build -c Release -- --verify-contracts

gen-interfaces: ## Extract interface documentation from code
	@echo "$(BLUE)Extracting interface documentation...$(NC)"
	@dotnet build $(DOCGEN_PROJECT) -c Release -v q
	@dotnet run --project $(DOCGEN_PROJECT) --no-build -c Release -- interfaces \
		--src src/MarketDataCollector \
		--output docs/generated/interfaces.md
	@echo "$(GREEN)Generated docs/generated/interfaces.md$(NC)"

gen-structure: ## Generate repository structure documentation
	@echo "$(BLUE)Generating repository structure documentation...$(NC)"
	@mkdir -p docs/generated
	@python3 build/scripts/docs/generate-structure-docs.py \
		--output docs/generated/repository-structure.md
	@echo "$(GREEN)Generated docs/generated/repository-structure.md$(NC)"

gen-providers: ## Generate provider registry documentation
	@echo "$(BLUE)Generating provider registry documentation...$(NC)"
	@mkdir -p docs/generated
	@python3 build/scripts/docs/generate-structure-docs.py \
		--output docs/generated/provider-registry.md \
		--providers-only \
		--extract-attributes
	@echo "$(GREEN)Generated docs/generated/provider-registry.md$(NC)"

gen-workflows: ## Generate workflows overview documentation
	@echo "$(BLUE)Generating workflows overview documentation...$(NC)"
	@mkdir -p docs/generated
	@python3 build/scripts/docs/generate-structure-docs.py \
		--output docs/generated/workflows-overview.md \
		--workflows-only
	@echo "$(GREEN)Generated docs/generated/workflows-overview.md$(NC)"

update-claude-md: gen-structure ## Update CLAUDE.md repository structure
	@echo "$(BLUE)Updating CLAUDE.md repository structure...$(NC)"
	@python3 build/scripts/docs/update-claude-md.py \
		--claude-md CLAUDE.md \
		--structure-source docs/generated/repository-structure.md
	@echo "$(GREEN)Updated CLAUDE.md$(NC)"

docs-all: gen-context gen-interfaces gen-structure gen-providers gen-workflows verify-adrs ## Generate all documentation
	@echo "$(GREEN)All documentation generated$(NC)"

# =============================================================================
# Diagnostics
# =============================================================================

doctor: ## Run environment health check
	@$(BUILDCTL) doctor

doctor-ci: ## Run environment health check for CI (warnings don't fail)
	@$(BUILDCTL) doctor --no-fail-on-warn

doctor-quick: ## Run quick environment check
	@$(BUILDCTL) doctor --quick

doctor-fix: ## Run environment check and auto-fix issues
	@echo "$(YELLOW)Auto-fix not yet implemented in buildctl doctor$(NC)"
	@$(BUILDCTL) doctor

diagnose: ## Run build diagnostics (alias)
	@$(BUILDCTL) build --project $(PROJECT) --configuration Release

diagnose-build: ## Run full build diagnostics
	@$(BUILDCTL) build --project $(PROJECT) --configuration Release

collect-debug: ## Collect debug bundle for issue reporting
	@$(BUILDCTL) collect-debug --project $(PROJECT) --configuration Release

collect-debug-minimal: ## Collect minimal debug bundle (no config/logs)
	@$(BUILDCTL) collect-debug --project $(PROJECT) --configuration Release

build-profile: ## Build with timing information
	@$(BUILDCTL) build-profile

build-binlog: ## Build with MSBuild binary log for detailed analysis
	@echo "$(BLUE)Building with binary log...$(NC)"
	@dotnet build $(PROJECT) -c Release /bl:msbuild.binlog
	@echo ""
	@echo "$(GREEN)Binary log created: msbuild.binlog$(NC)"
	@echo "To analyze, install MSBuild Structured Log Viewer:"
	@echo "  dotnet tool install -g MSBuild.StructuredLogger"
	@echo "  structuredlogviewer msbuild.binlog"

validate-data: ## Validate JSONL data integrity
	@$(BUILDCTL) validate-data --directory data/

analyze-errors: ## Analyze build output for known error patterns
	@echo "$(BLUE)Building and analyzing for known errors...$(NC)"
	@dotnet build $(PROJECT) 2>&1 | $(BUILDCTL) analyze-errors

build-graph: ## Generate dependency graph
	@$(BUILDCTL) build-graph --project $(PROJECT)

fingerprint: ## Generate build fingerprint
	@$(BUILDCTL) fingerprint --configuration Release

env-capture: ## Capture environment snapshot (NAME required)
	@$(BUILDCTL) env-capture $(NAME)

env-diff: ## Compare two environment snapshots
	@$(BUILDCTL) env-diff $(ENV1) $(ENV2)

impact: ## Analyze build impact for a file (FILE required)
	@$(BUILDCTL) impact --file $(FILE)

bisect: ## Run build bisect (GOOD and BAD required)
	@$(BUILDCTL) bisect --good $(GOOD) --bad $(BAD)

metrics: ## Show build metrics summary
	@$(BUILDCTL) metrics

history: ## Show build history summary
	@$(BUILDCTL) history

# =============================================================================
# Desktop App
# =============================================================================

icons: ## Generate desktop app icons from SVG
	@echo "$(BLUE)Generating desktop app icons...$(NC)"
	@npm ci --silent
	@node build/node/generate-icons.mjs
	@echo "$(GREEN)Icons generated in src/MarketDataCollector.Uwp/Assets/$(NC)"

desktop: icons ## Build desktop app (Windows only)
	@echo "$(BLUE)Building desktop app...$(NC)"
ifeq ($(OS),Windows_NT)
	dotnet build $(DESKTOP_PROJECT) -c Release -r win-x64
else
	@echo "$(YELLOW)Desktop app build requires Windows. Use GitHub Actions for CI builds.$(NC)"
	@echo "The desktop app can be built on Windows with:"
	@echo "  dotnet build $(DESKTOP_PROJECT) -c Release -r win-x64"
endif

desktop-publish: icons ## Publish desktop app (Windows only)
	@echo "$(BLUE)Publishing desktop app...$(NC)"
ifeq ($(OS),Windows_NT)
	dotnet publish $(DESKTOP_PROJECT) -c Release -r win-x64 --self-contained true \
		-p:WindowsPackageType=MSIX \
		-p:AppxPackageDir=publish/desktop/ \
		$(MSIX_APPINSTALLER_FLAGS) \
		$(MSIX_SIGNING_FLAGS)
	@echo "$(GREEN)Published MSIX to publish/desktop/$(NC)"
else
	@echo "$(YELLOW)Desktop app publish requires Windows.$(NC)"
	@echo "Use GitHub Actions workflow 'Desktop App Build' for CI builds."
endif

build-wpf: ## Build WPF desktop app (Windows only)
	@echo "$(BLUE)Building WPF desktop app...$(NC)"
ifeq ($(OS),Windows_NT)
	dotnet build $(WPF_PROJECT) -c Release -r win-x64
else
	@echo "$(YELLOW)WPF build requires Windows. Use GitHub Actions for CI builds.$(NC)"
	@echo "Run on Windows: dotnet build $(WPF_PROJECT) -c Release -r win-x64"
endif

build-uwp: ## Build UWP desktop app (legacy, Windows only)
	@echo "$(BLUE)Building UWP desktop app...$(NC)"
ifeq ($(OS),Windows_NT)
	dotnet build $(DESKTOP_PROJECT) -c Release -r win-x64
else
	@echo "$(YELLOW)UWP build requires Windows. Use GitHub Actions for CI builds.$(NC)"
	@echo "Run on Windows: dotnet build $(DESKTOP_PROJECT) -c Release -r win-x64"
endif

test-desktop-services: ## Run desktop-focused regression tests
	@echo "$(BLUE)Running desktop-focused tests...$(NC)"
ifeq ($(OS),Windows_NT)
	@echo "Running WPF service tests..."
	dotnet test tests/MarketDataCollector.Wpf.Tests/MarketDataCollector.Wpf.Tests.csproj -c Release
	@echo "Running UI service tests..."
	dotnet test tests/MarketDataCollector.Ui.Tests/MarketDataCollector.Ui.Tests.csproj -c Release
	@echo "Running integration tests..."
	dotnet test $(TEST_PROJECT) -c Release --filter "FullyQualifiedName~UwpCoreIntegrationTests|FullyQualifiedName~ConfigurationUnificationTests|FullyQualifiedName~CliModeResolverTests"
else
	@echo "$(YELLOW)Desktop service tests require Windows. Skipping WPF and UI tests.$(NC)"
	@echo "Running available integration tests..."
	dotnet test $(TEST_PROJECT) -c Release --filter "FullyQualifiedName~ConfigurationUnificationTests|FullyQualifiedName~CliModeResolverTests"
endif

desktop-dev-bootstrap: ## Run desktop development bootstrap checks (PowerShell)
	@echo "$(BLUE)Running desktop development bootstrap checks...$(NC)"
	pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev/desktop-dev.ps1

uwp-xaml-diagnose: ## Run UWP XAML preflight diagnostics (PowerShell)
	@echo "$(BLUE)Running UWP XAML diagnostics...$(NC)"
	pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev/diagnose-uwp-xaml.ps1

# =============================================================================
# AI Repository Updater
# =============================================================================

AI_UPDATER := python3 build/scripts/ai-repo-updater.py

ai-audit: ## Run full AI repository audit (all analysers)
	@echo "$(BLUE)Running full repository audit...$(NC)"
	@$(AI_UPDATER) audit --summary

ai-audit-code: ## Run AI code conventions audit
	@echo "$(BLUE)Auditing code conventions...$(NC)"
	@$(AI_UPDATER) audit-code --summary

ai-audit-docs: ## Run AI documentation quality audit
	@echo "$(BLUE)Auditing documentation quality...$(NC)"
	@$(AI_UPDATER) audit-docs --summary

ai-audit-tests: ## Run AI test coverage gap audit
	@echo "$(BLUE)Auditing test coverage gaps...$(NC)"
	@$(AI_UPDATER) audit-tests --summary

ai-verify: ## Run build + test + lint verification
	@echo "$(BLUE)Running verification (build + test + lint)...$(NC)"
	@$(AI_UPDATER) verify

ai-report: ## Generate AI improvement report
	@echo "$(BLUE)Generating improvement report...$(NC)"
	@$(AI_UPDATER) report --output docs/generated/improvement-report.md
	@echo "$(GREEN)Report written to docs/generated/improvement-report.md$(NC)"
