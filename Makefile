.DEFAULT_GOAL := help

.PHONY: help restore build run watch test test-unit test-js smoke test-all clean publish

help: ## 顯示可用命令清單
	@echo "禾境物業管理系統 - 常用操作指令："
	@echo ""
	@awk 'BEGIN {FS = ":.*?## "} /^[a-zA-Z_-]+:.*?## / {printf "  \033[36m%-12s\033[0m %s\n", $$1, $$2}' $(MAKEFILE_LIST)
	@echo ""

restore: ## 還原 NuGet 套件相依性
	dotnet restore

build: ## 建置專案（要求零警告、零錯誤）
	dotnet build

run: ## 啟動本機開發伺服器 (http://localhost:5188)
	dotnet run --launch-profile http

watch: ## 啟動熱重載開發伺服器 (dotnet watch)
	dotnet watch --launch-profile http

test-unit: ## 執行 xUnit 單元測試
	dotnet test tests/PropertyManagement.Tests/PropertyManagement.Tests.csproj

test-js: ## 執行前端 JavaScript 單元測試 (Node.js)
	node --test tests/body-regions.test.mjs

smoke: build ## 執行端到端 HTTP 整合測試 (Python 3)
	python3 tests/smoke.py

test: test-unit test-js ## 執行所有單元測試（xUnit 與 Node.js）

test-all: test-unit test-js smoke ## 執行完整測試套件（單元測試 + 端到端整合測試）

clean: ## 清理建置產物 (bin/ 與 obj/)
	dotnet clean
	rm -rf bin obj tests/PropertyManagement.Tests/bin tests/PropertyManagement.Tests/obj dist

publish: ## 發布 Release 版本產物至 dist/
	dotnet publish -c Release -o dist
