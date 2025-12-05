# Contributing to Pit Wall

Thank you for your interest in contributing to Pit Wall! This document provides guidelines and instructions for contributing.

## Development Setup

### Prerequisites

- Windows 10/11
- .NET SDK 8.0 or later
- SimHub 9.x installed
- Git
- Visual Studio Code (recommended) or Visual Studio 2022

### Getting Started

1. **Fork and Clone**
   ```bash
   git clone https://github.com/YOUR_USERNAME/PitWall.git
   cd PitWall
   ```

2. **Install SimHub SDK**
   - Locate your SimHub installation (typically `C:\Program Files (x86)\SimHub\`)
   - Copy the following DLLs to the `SimHub/` folder:
     - `SimHub.Plugins.dll`
     - `GameReaderCommon.dll`
     - `WoteverCommon.dll`
   - See `SimHub/README.md` for details

3. **Restore and Build**
   ```bash
   dotnet restore
   dotnet build --configuration Release
   ```

4. **Run Tests**
   ```bash
   dotnet test
   ```

All tests should pass before you begin development.

## Development Workflow

We follow **Test-Driven Development (TDD)**:

### TDD Cycle

1. **Red** - Write a failing test for the feature you want to add
2. **Green** - Write minimal code to make the test pass
3. **Refactor** - Clean up the code while keeping tests passing

### Example

```csharp
// 1. RED - Write the test first
[Fact]
public void CalculateFuelUsed_ReturnsCorrectValue()
{
    var strategy = new FuelStrategy();
    var result = strategy.CalculateFuelUsed(100.0, 95.0);
    Assert.Equal(5.0, result, 0.01);
}

// 2. GREEN - Implement minimal code
public double CalculateFuelUsed(double startFuel, double endFuel)
{
    return startFuel - endFuel;
}

// 3. REFACTOR - Improve if needed
```

## Code Style

### C# Conventions

- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use PascalCase for public members, camelCase for private
- Add XML documentation comments for public APIs
- Use meaningful variable and method names
- Keep methods focused and short (<30 lines when possible)

### Formatting

- Use Visual Studio Code auto-format (Alt+Shift+F)
- 4 spaces for indentation (not tabs)
- Opening braces on same line (K&R style)
- Enable nullable reference types

### Example

```csharp
namespace PitWall.Strategy
{
    /// <summary>
    /// Calculates fuel consumption and predicts pit stops
    /// </summary>
    public class FuelStrategy : IFuelStrategy
    {
        private readonly List<double> _fuelUsageHistory = new();

        /// <summary>
        /// Records fuel used in a completed lap
        /// </summary>
        /// <param name="fuelUsed">Amount of fuel consumed in liters</param>
        public void RecordLap(double fuelUsed)
        {
            _fuelUsageHistory.Add(fuelUsed);
        }
    }
}
```

## Testing Guidelines

### Test Organization

- One test class per production class
- Name tests descriptively: `MethodName_Scenario_ExpectedResult`
- Use Arrange-Act-Assert pattern
- Keep tests independent and isolated

### Coverage Goals

- Aim for >85% code coverage
- 100% coverage for strategy logic
- Focus on behavior, not implementation details

### Mock Usage

```csharp
// Use MockTelemetryBuilder for test data
var telemetry = MockTelemetryBuilder.GT3()
    .WithFuelRemaining(25.0)
    .WithLapTime(120.5)
    .Build();

// Use Moq for interface mocking when needed
var mockAudio = new Mock<IAudioEngine>();
mockAudio.Setup(m => m.Play(It.IsAny<string>()));
```

## Performance Requirements

All code must meet these performance targets:

- **CPU Usage**: <5% during active race
- **DataUpdate Method**: <10ms per call
- **Memory**: <50MB total plugin memory
- **Test Suite**: <5 seconds total runtime

### Performance Testing

Performance tests run automatically with every build:

```csharp
[Fact]
public void Plugin_DataUpdate_CompletesWithin10ms()
{
    var plugin = new PitWallPlugin();
    var stopwatch = Stopwatch.StartNew();
    plugin.DataUpdate(mockManager, ref gameData);
    stopwatch.Stop();
    
    Assert.True(stopwatch.ElapsedMilliseconds < 10);
}
```

## Pull Request Process

1. **Create a Feature Branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make Changes Following TDD**
   - Write tests first
   - Implement features
   - Ensure all tests pass
   - Ensure no warnings in build

3. **Commit with Clear Messages**
   ```bash
   git commit -m "feat: Add fuel consumption tracking

   - Implement FuelStrategy.RecordLap()
   - Add tests for fuel calculation
   - Update documentation
   
   Closes #123"
   ```

4. **Push and Create PR**
   ```bash
   git push origin feature/your-feature-name
   ```
   - Go to GitHub and create a Pull Request
   - Fill out the PR template
   - Link related issues

5. **Code Review**
   - Address reviewer feedback
   - Ensure CI passes
   - Maintain test coverage

### Commit Message Format

Follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation changes
- `test:` - Adding or updating tests
- `refactor:` - Code refactoring
- `perf:` - Performance improvements
- `chore:` - Maintenance tasks

## Project Phases

We're developing in phases following the [TDD Roadmap](TDD_ROADMAP.md):

- **Phase 0** ✅ - Environment & Scaffolding (COMPLETE)
- **Phase 1** 🚧 - Fuel Strategy (IN PROGRESS)
- **Phase 2** - Audio System
- **Phase 3** - Tyre Strategy
- **Phase 4** - Multi-Class Awareness
- **Phase 5+** - Advanced Features

Check the roadmap before starting work to align with current priorities.

## Getting Help

- **Questions?** Open a [Discussion](https://github.com/polecatspeaks/PitWall/discussions)
- **Bug Reports** Use the [Issue Tracker](https://github.com/polecatspeaks/PitWall/issues)
- **Feature Ideas** Start a discussion first

## Code of Conduct

### Our Standards

- Be respectful and inclusive
- Welcome newcomers
- Focus on constructive feedback
- Assume good intent
- Keep discussions professional

### Unacceptable Behavior

- Harassment or discrimination
- Trolling or insulting comments
- Spam or off-topic posts

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

## Recognition

Contributors will be recognized in:
- README.md acknowledgments
- Release notes
- Git commit history

Thank you for helping make Pit Wall better! 🏁
