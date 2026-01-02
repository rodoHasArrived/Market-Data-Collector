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
        setup-config lint benchmark

# Default target
.DEFAULT_GOAL := help

# Project settings
PROJECT := src/MarketDataCollector/MarketDataCollector.csproj
UI_PROJECT := src/MarketDataCollector.Ui/MarketDataCollector.Ui.csproj
TEST_PROJECT := tests/MarketDataCollector.Tests/MarketDataCollector.Tests.csproj
BENCHMARK_PROJECT := benchmarks/MarketDataCollector.Benchmarks/MarketDataCollector.Benchmarks.csproj
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
	@echo "$(BLUE)Publishing:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | grep -E 'publish' | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-18s$(NC) %s\n", $$1, $$2}'
	@echo ""

# =============================================================================
# Installation
# =============================================================================

install: ## Interactive installation (Docker or Native)
	@./install.sh

install-docker: ## Docker-based installation
	@./install.sh --docker

install-native: ## Native .NET installation
	@./install.sh --native

setup-config: ## Create appsettings.json from template
	@if [ ! -f appsettings.json ]; then \
		cp appsettings.sample.json appsettings.json; \
		echo "$(GREEN)Created appsettings.json$(NC)"; \
		echo "$(YELLOW)Remember to edit with your API credentials$(NC)"; \
	else \
		echo "$(YELLOW)appsettings.json already exists$(NC)"; \
	fi
	@mkdir -p data logs

check-deps: ## Check prerequisites
	@./install.sh --check

# =============================================================================
# Docker
# =============================================================================

docker: docker-build docker-up ## Build and start Docker container

docker-build: ## Build Docker image
	@echo "$(BLUE)Building Docker image...$(NC)"
	docker build -t $(DOCKER_IMAGE) .

docker-up: setup-config ## Start Docker container
	@echo "$(BLUE)Starting Docker container...$(NC)"
	docker compose up -d
	@echo "$(GREEN)Container started!$(NC)"
	@echo "  Dashboard: http://localhost:$(HTTP_PORT)"
	@echo "  Health:    http://localhost:$(HTTP_PORT)/health"
	@echo "  Metrics:   http://localhost:$(HTTP_PORT)/metrics"

docker-down: ## Stop Docker container
	docker compose down

docker-logs: ## View Docker logs
	docker compose logs -f

docker-restart: ## Restart Docker container
	docker compose restart

docker-clean: ## Remove Docker containers and images
	docker compose down -v
	docker rmi $(DOCKER_IMAGE) 2>/dev/null || true

docker-monitoring: ## Start with Prometheus and Grafana
	docker compose --profile monitoring up -d
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
	./publish.sh

publish-linux: ## Publish for Linux x64
	./publish.sh linux-x64

publish-windows: ## Publish for Windows x64
	./publish.sh win-x64

publish-macos: ## Publish for macOS x64
	./publish.sh osx-x64

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
