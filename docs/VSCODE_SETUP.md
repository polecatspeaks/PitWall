# VS Code Setup for PitWall Development

## Required Extensions

VS Code will automatically prompt you to install these when you open the project. Click "Install All" when prompted.

### Essential Extensions

1. **C# Dev Kit** (`ms-dotnettools.csdevkit`)
   - Full C# development suite
   - IntelliSense, debugging, and project management
   - Required for .NET development

2. **C#** (`ms-dotnettools.csharp`)
   - Core C# language support
   - Syntax highlighting and basic IntelliSense

3. **.NET Runtime** (`ms-dotnettools.vscode-dotnet-runtime`)
   - Runtime support for .NET tools in VS Code

### Testing Extensions

4. **.NET Test Explorer** (`formulahendry.dotnet-test-explorer`)
   - Visual test runner in sidebar
   - Run/debug individual tests
   - See test results inline

5. **Coverage Gutters** (`ryanluker.vscode-coverage-gutters`)
   - Show code coverage in editor gutter
   - Highlights covered/uncovered lines
   - Run tests with coverage: `dotnet test /p:CollectCoverage=true`

### Productivity Extensions

6. **GitHub Copilot** (`github.copilot`)
   - AI-powered code completion
   - Especially helpful for TDD test writing

7. **GitHub Copilot Chat** (`github.copilot-chat`)
   - AI assistant for code questions
   - Explain code, generate tests, refactor

8. **IntelliCode** (`visualstudioexptteam.vscodeintellicode`)
   - AI-assisted IntelliSense
   - Smart completions based on patterns

### Code Quality Extensions

9. **Code Spell Checker** (`streetsidesoftware.code-spell-checker`)
   - Catches typos in comments and docs
   - Custom dictionary for racing terms

10. **EditorConfig** (`editorconfig.editorconfig`)
    - Enforces consistent code style
    - Respects project formatting rules

11. **Code Runner** (`formulahendry.code-runner`)
    - Quick run commands
    - Useful for testing snippets

## Installation

### Automatic (Recommended)

1. Open the project in VS Code
2. VS Code will show a popup: "This workspace has extension recommendations"
3. Click **"Install All"**
4. Restart VS Code after installation

### Manual

```bash
# Install all at once via command line
code --install-extension ms-dotnettools.csdevkit
code --install-extension ms-dotnettools.csharp
code --install-extension ms-dotnettools.vscode-dotnet-runtime
code --install-extension formulahendry.dotnet-test-explorer
code --install-extension ryanluker.vscode-coverage-gutters
code --install-extension github.copilot
code --install-extension github.copilot-chat
code --install-extension visualstudioexptteam.vscodeintellicode
code --install-extension streetsidesoftware.code-spell-checker
code --install-extension editorconfig.editorconfig
code --install-extension formulahendry.code-runner
```

## Workspace Settings

The project includes pre-configured settings in `.vscode/settings.json`:

- ✅ Format on save enabled
- ✅ Auto-organize imports
- ✅ 4-space indentation for C#
- ✅ Test explorer configured
- ✅ Coverage display enabled
- ✅ bin/obj folders hidden
- ✅ Custom spell-check dictionary

## Keyboard Shortcuts

### Building & Testing

| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+B` | Build project |
| `Ctrl+Shift+T` | Run all tests |
| `F5` | Debug tests |
| `Ctrl+F5` | Run tests without debugging |

### Editing

| Shortcut | Action |
|----------|--------|
| `Alt+Shift+F` | Format document |
| `Ctrl+.` | Quick fix / code actions |
| `F12` | Go to definition |
| `Shift+F12` | Find all references |
| `Ctrl+Space` | Trigger IntelliSense |

### Copilot

| Shortcut | Action |
|----------|--------|
| `Ctrl+I` | Open Copilot Chat |
| `Tab` | Accept Copilot suggestion |
| `Alt+]` | Next suggestion |
| `Alt+[` | Previous suggestion |

## Debugging

### Debug Tests

1. Open a test file (e.g., `PluginLifecycleTests.cs`)
2. Click on test method name
3. Press `F5` or click "Debug Test" CodeLens
4. Set breakpoints as needed

### Attach to SimHub

1. Start SimHub with your plugin loaded
2. Press `F5` in VS Code
3. Select **".NET: Attach to SimHub"**
4. Choose `SimHubWPF.exe` from the process list
5. Set breakpoints in your plugin code

## Tasks

Access via `Ctrl+Shift+P` → "Tasks: Run Task":

- **build** - Build main project
- **test** - Run all tests
- **watch-test** - Auto-run tests on file changes (TDD mode!)
- **build-release** - Build for release
- **clean** - Clean build artifacts
- **restore** - Restore NuGet packages

## Test Explorer

The Test Explorer sidebar shows all tests:

1. Click Testing icon in sidebar (beaker icon)
2. Expand `PitWall.Tests`
3. Run individual tests by clicking play button
4. Debug tests by clicking debug button
5. Filter tests with search box

## Coverage Gutters

After running tests with coverage:

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

1. Click "Watch" in status bar
2. Green lines = covered
3. Red lines = not covered
4. Goal: >85% coverage

## Tips

### TDD Workflow in VS Code

1. **Write test** - Use Copilot to help generate test structure
2. **Run test** - Click CodeLens "Run Test" or `Ctrl+Shift+T`
3. **See it fail** (Red)
4. **Write code** - Implement feature
5. **Run test again** - Should pass (Green)
6. **Refactor** - Clean up with test still passing
7. **Check coverage** - Use Coverage Gutters

### Using Copilot for TDD

Ask Copilot Chat (`Ctrl+I`):
- "Generate a test for CalculateFuelUsed method"
- "What edge cases should I test?"
- "Refactor this while keeping tests passing"
- "Explain what this test is checking"

### Performance Tips

- Keep Test Explorer panel collapsed when not using
- Use "watch-test" task during active TDD
- Build in Release mode for performance tests
- Close other heavy applications during testing

## Troubleshooting

### IntelliSense not working

1. Reload window: `Ctrl+Shift+P` → "Developer: Reload Window"
2. Restore packages: `dotnet restore`
3. Check OmniSharp output: View → Output → Select "C#"

### Tests not discovered

1. Build test project: `dotnet build PitWall.Tests`
2. Restart Test Explorer
3. Check test output for errors

### Coverage not showing

1. Ensure `coverlet.collector` package is installed
2. Run with coverage flag: `/p:CollectCoverage=true`
3. Click "Watch" in Coverage Gutters status bar

## Additional Resources

- [C# Dev Kit Docs](https://code.visualstudio.com/docs/csharp/get-started)
- [Testing in VS Code](https://code.visualstudio.com/docs/csharp/testing)
- [GitHub Copilot Docs](https://docs.github.com/en/copilot)

---

**Ready to code!** All extensions configured for optimal TDD workflow. 🚀
