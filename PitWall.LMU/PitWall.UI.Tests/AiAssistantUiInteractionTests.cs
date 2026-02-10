using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using PitWall.UI.Models;
using PitWall.UI.ViewModels;
using PitWall.UI.Views;
using Xunit;

namespace PitWall.UI.Tests
{
    [Collection("Avalonia UI Tests")]
    public class AiAssistantUiInteractionTests
    {
        [Fact]
        public void AiInput_TextBox_AcceptsInput()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.AiInput = "Should I pit soon?";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Should I pit soon?", viewModel.AiInput);

            window.Close();
        }

        [Fact]
        public void AiInput_EmptyString_AcceptsInput()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.AiInput = "test";
            viewModel.AiInput = "";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("", viewModel.AiInput);

            window.Close();
        }

        [Fact]
        public void AiInput_LongText_AcceptsInput()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            var longText = new string('a', 1000);
            viewModel.AiInput = longText;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(longText, viewModel.AiInput);

            window.Close();
        }

        [Fact]
        public void AiInput_SpecialCharacters_AcceptsInput()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            var specialText = "Hello! @#$%^&*() <test> {brackets} [array]";
            viewModel.AiInput = specialText;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(specialText, viewModel.AiInput);

            window.Close();
        }

        [Fact]
        public void AiMessages_Collection_StartsEmpty()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            Assert.Empty(viewModel.AiMessages);

            window.Close();
        }

        [Fact]
        public void AiMessages_CanAddMessages()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.AiMessages.Add(new AiMessage
            {
                Role = "User",
                Text = "Test message"
            });

            Dispatcher.UIThread.RunJobs();

            Assert.Single(viewModel.AiMessages);
            Assert.Equal("User", viewModel.AiMessages[0].Role);
            Assert.Equal("Test message", viewModel.AiMessages[0].Text);

            window.Close();
        }

        [Fact]
        public void AiMessages_CanAddMultipleMessages()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            for (int i = 0; i < 10; i++)
            {
                viewModel.AiMessages.Add(new AiMessage
                {
                    Role = i % 2 == 0 ? "User" : "Assistant",
                    Text = $"Message {i}"
                });
            }

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(10, viewModel.AiMessages.Count);

            window.Close();
        }

        [Fact]
        public void AiMessages_CanClearMessages()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.AiMessages.Add(new AiMessage { Role = "User", Text = "Test" });
            Assert.Single(viewModel.AiMessages);

            viewModel.AiMessages.Clear();
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(viewModel.AiMessages);

            window.Close();
        }

        [Fact]
        public void SendButton_Exists()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            var sendButton = window.GetLogicalDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Content?.ToString() == "Send");

            Assert.NotNull(sendButton);
            Assert.NotNull(sendButton.Command);

            window.Close();
        }

        [Fact]
        public void AiInputTextBox_Exists()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            var inputBox = window.GetLogicalDescendants().OfType<TextBox>()
                .FirstOrDefault(tb => tb.Watermark == "Ask the race engineer...");

            Assert.NotNull(inputBox);

            window.Close();
        }

        [Fact]
        public void AiMessagesItemsControl_Exists()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            var itemsControl = window.GetLogicalDescendants().OfType<ItemsControl>().FirstOrDefault();

            Assert.NotNull(itemsControl);

            window.Close();
        }
    }
}
