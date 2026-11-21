# Contributing to Bruinet

Thank you for your interest in contributing to Bruinet! This document provides guidelines and information for contributors.

## Code of Conduct

By participating in this project, you agree to maintain a respectful and inclusive environment for everyone.

## How to Contribute

### Reporting Bugs

1. Check existing issues to avoid duplicates
2. Use the bug report template
3. Provide a minimal reproduction case
4. Include system information (OS, .NET version, etc.)

### Suggesting Features

1. Check existing issues and discussions
2. Describe the use case and expected behavior
3. Consider how it fits with the existing architecture

### Pull Requests

1. Fork the repository
2. Create a feature branch from `develop`
3. Write tests for new functionality
4. Ensure all tests pass
5. Follow the code style guidelines
6. Submit a pull request to `develop`

## Development Setup

### Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 or VS Code with C# extension
- Windows (for WPF testing)

### Building

```bash
git clone https://github.com/YOUR_USERNAME/Bruinet.git
cd Bruinet
dotnet restore
dotnet build
```

### Running Tests

```bash
dotnet test
```

## Code Style Guidelines

- Use file-scoped namespaces
- Follow Microsoft C# coding conventions
- Use meaningful names for variables and methods
- Add XML documentation for public APIs
- Keep methods focused and small

## Project Structure

```
Bruinet/
├── src/
│   ├── Bruinet.Core/       # Core abstractions
│   ├── Bruinet.Wpf/        # WPF automation
│   ├── Bruinet.Html/       # Web automation
│   ├── Bruinet.Maui/       # Mobile automation
│   └── Bruinet.Api.Mocking/    # API mocking
├── tests/
│   └── ...                 # Test projects
├── samples/
│   └── ...                 # Sample applications
└── docs/
    └── ...                 # Documentation
```

## Commit Messages

Use conventional commit format:

- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation changes
- `test:` Test additions or changes
- `refactor:` Code refactoring
- `chore:` Maintenance tasks

## Questions?

Open a discussion on GitHub or reach out to the maintainers.
