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
        doctor doctor-quick doctor-fix diagnose diagnose-build diagnose-restore diagnose-clean \
        collect-debug collect-debug-minimal build-profile build-binlog validate-data analyze-errors

# Default target
.DEFAULT_GOAL := help

# Project settings
PROJECT := src/MarketDataCollector/MarketDataCollector.csproj
UI_PROJECT := src/MarketDataCollector.Ui/MarketDataCollector.Ui.csproj
TEST_PROJECT := tests/MarketDataCollector.Tests/MarketDataCollector.Tests.csproj
BENCHMARK_PROJECT := benchmarks/MarketDataCollector.Benchmarks/MarketDataCollector.Benchmarks.csproj
DOCGEN_PROJECT := tools/DocGenerator/DocGenerator.csproj
DOCKER_IMAGE := marketdatacollector:latest
HTTP_PORT ?= 8080

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
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | grep -E 'docs|verify-adr|verify-contract|gen-context' | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-18s$(NC) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BLUE)Publishing:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | grep -E 'publish' | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-18s$(NC) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BLUE)Diagnostics:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | grep -E 'doctor|diagnose|collect-debug|build-profile|build-binlog|validate-data|analyze-errors' | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-18s$(NC) %s\n", $$1, $$2}'
	@echo ""

# =============================================================================
# Installation
# =============================================================================

install: ## Interactive installation (Docker or Native)
	@./scripts/install/install.sh

install-docker: ## Docker-based installation
	@./scripts/install/install.sh --docker

install-native: ## Native .NET installation
	@./scripts/install/install.sh --native

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
	@./scripts/install/install.sh --check

# =============================================================================
# Docker
# =============================================================================

docker: docker-build docker-up ## Build and start Docker container

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
	@echo "$(BLUE)Building...$(NC)"
	dotnet build $(PROJECT) -c Release

run: setup-config ## Run the collector
	@echo "$(BLUE)Running collector...$(NC)"
	dotnet run --project $(PROJECT) -- --serve-status --watch-config

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

clean: ## Clean build artifacts
	@echo "$(BLUE)Cleaning...$(NC)"
	dotnet clean
	rm -rf bin/ obj/ publish/

# =============================================================================
# Publishing
# =============================================================================

publish: ## Publish for all platforms
	@echo "$(BLUE)Publishing for all platforms...$(NC)"
	./scripts/publish/publish.sh

publish-linux: ## Publish for Linux x64
	./scripts/publish/publish.sh linux-x64

publish-windows: ## Publish for Windows x64
	./scripts/publish/publish.sh win-x64

publish-macos: ## Publish for macOS x64
	./scripts/publish/publish.sh osx-x64

# =============================================================================
# Utilities
# =============================================================================

health: ## Check application health
	@curl -s http://localhost:$(HTTP_PORT)/health | jq . 2>/dev/null || echo "Application not running or jq not installed"

status: ## Get application status
	@curl -s http://localhost:$(HTTP_PORT)/status | jq . 2>/dev/null || echo "Application not running or jq not installed"

metrics: ## Get Prometheus metrics
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

# =============================================================================
# Diagnostics
# =============================================================================

doctor: ## Run environment health check
	@./scripts/diagnostics/doctor.sh

doctor-quick: ## Run quick environment check
	@./scripts/diagnostics/doctor.sh --quick

doctor-fix: ## Run environment check and auto-fix issues
	@./scripts/diagnostics/doctor.sh --fix

diagnose: ## Run build diagnostics (alias)
	@./scripts/diagnostics/diagnose-build.sh all

diagnose-build: ## Run full build diagnostics
	@./scripts/diagnostics/diagnose-build.sh all

diagnose-restore: ## Diagnose NuGet restore issues
	@./scripts/diagnostics/diagnose-build.sh restore

diagnose-clean: ## Clean and run diagnostics
	@./scripts/diagnostics/diagnose-build.sh clean

collect-debug: ## Collect debug bundle for issue reporting
	@./scripts/diagnostics/collect-debug.sh

collect-debug-minimal: ## Collect minimal debug bundle (no config/logs)
	@./scripts/diagnostics/collect-debug.sh --no-logs --no-config

build-profile: ## Build with timing information
	@echo "$(BLUE)Building with timing profile...$(NC)"
	@echo ""
	@START_TIME=$$(date +%s); \
	echo "$(YELLOW)Phase 1: Restore$(NC)"; \
	RESTORE_START=$$(date +%s); \
	dotnet restore $(PROJECT) -v q 2>/dev/null; \
	RESTORE_END=$$(date +%s); \
	RESTORE_TIME=$$((RESTORE_END - RESTORE_START)); \
	echo "  Completed in $${RESTORE_TIME}s"; \
	echo ""; \
	echo "$(YELLOW)Phase 2: Build$(NC)"; \
	BUILD_START=$$(date +%s); \
	dotnet build $(PROJECT) --no-restore -c Release -v q 2>/dev/null; \
	BUILD_END=$$(date +%s); \
	BUILD_TIME=$$((BUILD_END - BUILD_START)); \
	echo "  Completed in $${BUILD_TIME}s"; \
	echo ""; \
	END_TIME=$$(date +%s); \
	TOTAL_TIME=$$((END_TIME - START_TIME)); \
	echo "$(BLUE)━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━$(NC)"; \
	echo "$(GREEN)Build Performance Report$(NC)"; \
	echo "$(BLUE)━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━$(NC)"; \
	printf "  restore     %3ds\n" $$RESTORE_TIME; \
	printf "  build       %3ds\n" $$BUILD_TIME; \
	echo "  ─────────────────────────────────────────"; \
	printf "  $(GREEN)total       %3ds$(NC)\n" $$TOTAL_TIME

build-binlog: ## Build with MSBuild binary log for detailed analysis
	@echo "$(BLUE)Building with binary log...$(NC)"
	@dotnet build $(PROJECT) -c Release /bl:msbuild.binlog
	@echo ""
	@echo "$(GREEN)Binary log created: msbuild.binlog$(NC)"
	@echo "To analyze, install MSBuild Structured Log Viewer:"
	@echo "  dotnet tool install -g MSBuild.StructuredLogger"
	@echo "  structuredlogviewer msbuild.binlog"

validate-data: ## Validate JSONL data integrity
	@./scripts/diagnostics/validate-data.sh data/

analyze-errors: ## Analyze build output for known error patterns
	@echo "$(BLUE)Building and analyzing for known errors...$(NC)"
	@dotnet build $(PROJECT) 2>&1 | ./scripts/diagnostics/analyze-errors.sh
