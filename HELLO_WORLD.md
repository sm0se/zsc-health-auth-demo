# Hello World

Welcome to the ZSC Health Auth POC project!

This is a demonstration file showcasing the kenCode agent's pull request capabilities.

## About kenCode

kenCode is an expert software engineering agent with specialized capabilities for:

- **Code Exploration**: Grep, glob, and pattern matching across the repository
- **File Operations**: Reading, writing, and editing files with precision
- **Build & Test**: Running builds and test suites with full output capture
- **Git Integration**: Staging, committing, and creating pull requests programmatically
- **Process Management**: Starting long-lived services and monitoring their state
- **Browser Automation**: Interacting with web applications for integration testing
- **Delegation**: Offloading specialized analysis to focused sub-agents

## The ZSC Health Auth POC

This repository demonstrates an ASP.NET Core microservices architecture:

- **API Gateway** (Port 5080): Entry point for all requests
- **Interceptor** (Port 5100): Request interception and validation layer
- **Backend for Frontend (BFF)** (Port 5200): Aggregation and response formatting
- **Health Status Service** (Port 5300): Health monitoring and probing
- **Device API** (Port 5400): Device management endpoints

The architecture ensures proper authentication, authorization, and header propagation across the request chain.

## Getting Started

```bash
# Build all projects
dotnet build

# Run all services
scripts/run-all.sh

# Run tests
dotnet test

# Run end-to-end tests
ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests
```

---

*This file was created by kenCode to demonstrate PR creation and detailed commit workflows.*
