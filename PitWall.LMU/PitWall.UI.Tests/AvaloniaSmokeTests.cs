using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using PitWall.UI.ViewModels;
using PitWall.UI.Views;
using Xunit;

namespace PitWall.UI.Tests
{
    [Collection("Avalonia UI Tests")]
    public class AvaloniaSmokeTests
    {
        [Fact]
        public void MainWindow_CanLoadWithViewModel()
        {
            AvaloniaTestBootstrap.Ensure();
            var window = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };

            window.Show();

            Assert.NotNull(window.DataContext);
            window.Close();
        }

        [Fact]
        public void MainWindow_ContainsFuelPanel()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow
            {
                DataContext = viewModel
            };

            window.Show();

            // Fuel panel should have FUEL label
            var fuelLabels = window.GetLogicalDescendants().OfType<TextBlock>()
                .Where(tb => tb.Text == "FUEL");
            Assert.NotEmpty(fuelLabels);

            window.Close();
        }

        [Fact]
        public void MainWindow_ContainsTiresPanel()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow
            {
                DataContext = viewModel
            };

            window.Show();

            var tiresLabel = window.GetLogicalDescendants().OfType<TextBlock>()
                .Where(tb => tb.Text == "TIRES");
            Assert.NotEmpty(tiresLabel);

            window.Close();
        }

        [Fact]
        public void MainWindow_ContainsTimingPanel()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow
            {
                DataContext = viewModel
            };

            window.Show();

            var timingLabel = window.GetLogicalDescendants().OfType<TextBlock>()
                .Where(tb => tb.Text == "TIMING");
            Assert.NotEmpty(timingLabel);

            window.Close();
        }

        [Fact]
        public void MainWindow_ContainsAlertsPanel()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow
            {
                DataContext = viewModel
            };

            window.Show();

            var alertsLabels = window.GetLogicalDescendants().OfType<TextBlock>()
                .Where(tb => tb.Text == "ALERTS");
            Assert.NotEmpty(alertsLabels);

            window.Close();
        }

        [Fact]
        public void MainWindow_ContainsStrategyPanel()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow
            {
                DataContext = viewModel
            };

            window.Show();

            var strategyLabel = window.GetLogicalDescendants().OfType<TextBlock>()
                .Where(tb => tb.Text == "STRATEGY");
            Assert.NotEmpty(strategyLabel);

            window.Close();
        }

        [Fact]
        public void MainWindow_ContainsAiAssistantPanel()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow
            {
                DataContext = viewModel
            };

            window.Show();

            var aiLabel = window.GetLogicalDescendants().OfType<TextBlock>()
                .Where(tb => tb.Text == "AI ASSISTANT");
            Assert.NotEmpty(aiLabel);

            //Find ItemsControl for messages
            var itemsControl = window.GetLogicalDescendants().OfType<ItemsControl>().FirstOrDefault();
            Assert.NotNull(itemsControl);

            // Find TextBox for input
            var inputBox = window.GetLogicalDescendants().OfType<TextBox>()
                .FirstOrDefault(tb => tb.Watermark == "Ask the race engineer...");
            Assert.NotNull(inputBox);

            // Find Send button
            var sendButton = window.GetLogicalDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Content?.ToString() == "Send");
            Assert.NotNull(sendButton);

            window.Close();
        }

        [Fact]
        public void MainWindow_ContainsSettingsPanel()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow
            {
                DataContext = viewModel
            };

            window.Show();

            var settingsLabel = window.GetLogicalDescendants().OfType<TextBlock>()
                .Where(tb => tb.Text == "AGENT SETTINGS");
            Assert.NotEmpty(settingsLabel);

            var enableLlmCheckbox = window.GetLogicalDescendants().OfType<CheckBox>()
                .FirstOrDefault(cb => cb.Content?.ToString() == "Enable LLM");
            Assert.NotNull(enableLlmCheckbox);

            var reloadButton = window.GetLogicalDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Content?.ToString() == "Reload Config");
            Assert.NotNull(reloadButton);

            var saveButton = window.GetLogicalDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Content?.ToString() == "Save Config");
            Assert.NotNull(saveButton);

            window.Close();
        }

        [Fact]
        public void MainWindow_ContainsLapDisplay()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow
            {
                DataContext = viewModel
            };

            window.Show();

            var lapLabel = window.GetLogicalDescendants().OfType<TextBlock>()
                .Where(tb => tb.Text == "LAP");
            Assert.NotEmpty(lapLabel);

            window.Close();
        }

        [Fact]
        public void MainWindow_ButtonsExist()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow
            {
                DataContext = viewModel
            };

            window.Show();

            var buttons = window.GetLogicalDescendants().OfType<Button>().ToList();
            Assert.NotEmpty(buttons);

            // Check for tab buttons
            var tabButtons = buttons.Where(b => 
                b.Content?.ToString() == "Telemetry" || 
                b.Content?.ToString() == "Competitors" || 
                b.Content?.ToString() == "AI Assistant" || 
                b.Content?.ToString() == "Settings").ToList();
            Assert.Equal(4, tabButtons.Count);

            window.Close();
        }
    }
}

[CollectionDefinition("Avalonia UI Tests", DisableParallelization = true)]
public class AvaloniaTestCollection
{
}

internal static class AvaloniaTestBootstrap
{
    private static bool _initialized;
    private static readonly object _lock = new object();

    public static void Ensure()
    {
        lock (_lock)
        {
            if (_initialized)
            {
                return;
            }

            AppBuilder.Configure<PitWall.UI.App>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .SetupWithoutStarting();

            _initialized = true;
        }
    }
}

