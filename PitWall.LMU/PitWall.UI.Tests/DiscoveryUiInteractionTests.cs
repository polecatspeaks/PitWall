using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using PitWall.UI.ViewModels;
using PitWall.UI.Views;
using Xunit;

namespace PitWall.UI.Tests
{
    [Collection("Avalonia UI Tests")]
    public class DiscoveryUiInteractionTests
    {
        [Fact]
        public void RunDiscoveryButton_Exists()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            var discoveryButton = window.GetLogicalDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Content?.ToString() == "Run Discovery");

            Assert.NotNull(discoveryButton);
            Assert.NotNull(discoveryButton.Command);

            window.Close();
        }

        [Fact]
        public void RunDiscoveryButton_DisabledWhenDiscoveryOff()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.EnableLlmDiscovery = false;
            Dispatcher.UIThread.RunJobs();

            var discoveryButton = window.GetLogicalDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Content?.ToString() == "Run Discovery");

            Assert.NotNull(discoveryButton);
            Assert.False(discoveryButton.IsEnabled);

            window.Close();
        }

        [Fact]
        public void RunDiscoveryButton_EnabledWhenDiscoveryOn()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.EnableLlmDiscovery = true;
            Dispatcher.UIThread.RunJobs();

            var discoveryButton = window.GetLogicalDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Content?.ToString() == "Run Discovery");

            Assert.NotNull(discoveryButton);
            Assert.True(discoveryButton.IsEnabled);

            window.Close();
        }

        [Fact]
        public void DiscoveryResultsMessage_StartsEmpty()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            Assert.Equal(string.Empty, viewModel.DiscoveryResultsMessage);

            window.Close();
        }

        [Fact]
        public void DiscoveryResultsMessage_CanBeSet()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.DiscoveryResultsMessage = "Found 3 endpoint(s)";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Found 3 endpoint(s)", viewModel.DiscoveryResultsMessage);

            window.Close();
        }

        [Fact]
        public void RunDiscoveryCommand_Exists()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();

            Assert.NotNull(viewModel.RunDiscoveryCommand);
        }

        [Fact]
        public void DiscoveryResultsTextBlock_Exists()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.DiscoveryResultsMessage = "Test message";
            Dispatcher.UIThread.RunJobs();

            var resultsTextBlock = window.GetLogicalDescendants().OfType<TextBlock>()
                .FirstOrDefault(tb => tb.Text == "Test message");

            Assert.NotNull(resultsTextBlock);

            window.Close();
        }
    }
}
