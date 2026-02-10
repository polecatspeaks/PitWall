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
    public class SettingsUiInteractionTests
    {
        [Fact]
        public void EnableLlmDiscovery_Checkbox_TogglesWithoutCrash()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            var checkbox = window.GetLogicalDescendants().OfType<CheckBox>()
                .FirstOrDefault(cb => cb.Content?.ToString()?.Contains("Enable LLM Discovery") == true);

            Assert.NotNull(checkbox);

            // Test toggling on
            checkbox.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(viewModel.EnableLlmDiscovery);

            // Test toggling off
            checkbox.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.False(viewModel.EnableLlmDiscovery);

            // Test multiple rapid toggles
            for (int i = 0; i < 10; i++)
            {
                checkbox.IsChecked = i % 2 == 0;
                Dispatcher.UIThread.RunJobs();
            }

            window.Close();
        }

        [Fact]
        public void EnableLlm_Checkbox_TogglesWithoutCrash()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            var checkbox = window.GetLogicalDescendants().OfType<CheckBox>()
                .FirstOrDefault(cb => cb.Content?.ToString() == "Enable LLM");

            Assert.NotNull(checkbox);

            checkbox.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(viewModel.EnableLlm);

            checkbox.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.False(viewModel.EnableLlm);

            window.Close();
        }

        [Fact]
        public void RequirePitForLlm_Checkbox_TogglesWithoutCrash()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            var checkbox = window.GetLogicalDescendants().OfType<CheckBox>()
                .FirstOrDefault(cb => cb.Content?.ToString() == "Require pit for LLM");

            Assert.NotNull(checkbox);

            checkbox.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(viewModel.RequirePitForLlm);

            checkbox.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.False(viewModel.RequirePitForLlm);

            window.Close();
        }

        [Fact]
        public void LlmProvider_ComboBox_ChangesWithoutCrash()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            var comboBox = window.GetLogicalDescendants().OfType<ComboBox>().FirstOrDefault();
            Assert.NotNull(comboBox);

            // Test each provider
            foreach (var provider in new[] { "Ollama", "OpenAI", "Anthropic" })
            {
                comboBox.SelectedItem = provider;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(provider, viewModel.LlmProvider);
            }

            window.Close();
        }

        [Fact]
        public void LlmEndpoint_TextBox_AcceptsInput()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.LlmEndpoint = "http://localhost:11434";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("http://localhost:11434", viewModel.LlmEndpoint);

            viewModel.LlmEndpoint = "";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("", viewModel.LlmEndpoint);

            window.Close();
        }

        [Fact]
        public void LlmModel_TextBox_AcceptsInput()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.LlmModel = "llama3";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("llama3", viewModel.LlmModel);

            viewModel.LlmModel = "gpt-4";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("gpt-4", viewModel.LlmModel);

            window.Close();
        }

        [Fact]
        public void LlmTimeoutMs_TextBox_AcceptsNumericStrings()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.LlmTimeoutMs = "30000";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("30000", viewModel.LlmTimeoutMs);

            viewModel.LlmTimeoutMs = "60000";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("60000", viewModel.LlmTimeoutMs);

            window.Close();
        }

        [Fact]
        public void DiscoveryPort_TextBox_AcceptsNumericStrings()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.LlmDiscoveryPort = "11434";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("11434", viewModel.LlmDiscoveryPort);

            viewModel.LlmDiscoveryPort = "8080";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("8080", viewModel.LlmDiscoveryPort);

            window.Close();
        }

        [Fact]
        public void DiscoveryMaxConcurrency_TextBox_AcceptsNumericStrings()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.LlmDiscoveryMaxConcurrency = "10";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("10", viewModel.LlmDiscoveryMaxConcurrency);

            viewModel.LlmDiscoveryMaxConcurrency = "5";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("5", viewModel.LlmDiscoveryMaxConcurrency);

            window.Close();
        }

        [Fact]
        public void DiscoverySubnetPrefix_TextBox_AcceptsInput()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.LlmDiscoverySubnetPrefix = "192.168.1";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("192.168.1", viewModel.LlmDiscoverySubnetPrefix);

            viewModel.LlmDiscoverySubnetPrefix = "10.0.0";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("10.0.0", viewModel.LlmDiscoverySubnetPrefix);

            window.Close();
        }

        [Fact]
        public void OpenAiFields_AcceptInput()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.OpenAiEndpoint = "https://api.openai.com/v1";
            viewModel.OpenAiModel = "gpt-4";
            viewModel.OpenAiApiKey = "sk-test123";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("https://api.openai.com/v1", viewModel.OpenAiEndpoint);
            Assert.Equal("gpt-4", viewModel.OpenAiModel);
            Assert.Equal("sk-test123", viewModel.OpenAiApiKey);

            window.Close();
        }

        [Fact]
        public void AnthropicFields_AcceptInput()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            viewModel.AnthropicEndpoint = "https://api.anthropic.com";
            viewModel.AnthropicModel = "claude-3-opus";
            viewModel.AnthropicApiKey = "sk-ant-test456";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("https://api.anthropic.com", viewModel.AnthropicEndpoint);
            Assert.Equal("claude-3-opus", viewModel.AnthropicModel);
            Assert.Equal("sk-ant-test456", viewModel.AnthropicApiKey);

            window.Close();
        }

        [Fact]
        public void AllCheckboxes_CanBeToggled_Simultaneously()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            // Enable all checkboxes
            viewModel.EnableLlm = true;
            viewModel.RequirePitForLlm = true;
            viewModel.EnableLlmDiscovery = true;
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.EnableLlm);
            Assert.True(viewModel.RequirePitForLlm);
            Assert.True(viewModel.EnableLlmDiscovery);

            // Disable all checkboxes
            viewModel.EnableLlm = false;
            viewModel.RequirePitForLlm = false;
            viewModel.EnableLlmDiscovery = false;
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.EnableLlm);
            Assert.False(viewModel.RequirePitForLlm);
            Assert.False(viewModel.EnableLlmDiscovery);

            window.Close();
        }

        [Fact]
        public void AllTextBoxes_CanBePopulated_Simultaneously()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            // Fill all text fields
            viewModel.LlmEndpoint = "http://localhost:11434";
            viewModel.LlmModel = "llama3";
            viewModel.LlmTimeoutMs = "30000";
            viewModel.LlmDiscoveryPort = "11434";
            viewModel.LlmDiscoveryMaxConcurrency = "10";
            viewModel.LlmDiscoverySubnetPrefix = "192.168.1";
            viewModel.OpenAiEndpoint = "https://api.openai.com/v1";
            viewModel.OpenAiModel = "gpt-4";
            viewModel.OpenAiApiKey = "sk-test";
            viewModel.AnthropicEndpoint = "https://api.anthropic.com";
            viewModel.AnthropicModel = "claude-3-opus";
            viewModel.AnthropicApiKey = "sk-ant-test";

            Dispatcher.UIThread.RunJobs();

            // Verify all fields retained values
            Assert.Equal("http://localhost:11434", viewModel.LlmEndpoint);
            Assert.Equal("llama3", viewModel.LlmModel);
            Assert.Equal("30000", viewModel.LlmTimeoutMs);
            Assert.Equal("11434", viewModel.LlmDiscoveryPort);
            Assert.Equal("10", viewModel.LlmDiscoveryMaxConcurrency);
            Assert.Equal("192.168.1", viewModel.LlmDiscoverySubnetPrefix);

            window.Close();
        }

        [Fact]
        public void InvalidNumericInputs_DontCrash()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            // Test invalid inputs
            viewModel.LlmTimeoutMs = "not-a-number";
            viewModel.LlmDiscoveryPort = "abc123";
            viewModel.LlmDiscoveryMaxConcurrency = "xyz";

            Dispatcher.UIThread.RunJobs();

            // Should not crash, values are stored as strings
            Assert.Equal("not-a-number", viewModel.LlmTimeoutMs);
            Assert.Equal("abc123", viewModel.LlmDiscoveryPort);
            Assert.Equal("xyz", viewModel.LlmDiscoveryMaxConcurrency);

            window.Close();
        }

        [Fact]
        public void EmptyStringInputs_DontCrash()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            // Set all to empty strings
            viewModel.LlmEndpoint = "";
            viewModel.LlmModel = "";
            viewModel.LlmTimeoutMs = "";
            viewModel.LlmDiscoveryPort = "";
            viewModel.LlmDiscoveryMaxConcurrency = "";
            viewModel.LlmDiscoverySubnetPrefix = "";

            Dispatcher.UIThread.RunJobs();

            // Should not crash
            Assert.Equal("", viewModel.LlmEndpoint);
            Assert.Equal("", viewModel.LlmModel);

            window.Close();
        }

        [Fact]
        public void NullStringInputs_DontCrash()
        {
            AvaloniaTestBootstrap.Ensure();
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            // Set nullable fields to null
            viewModel.LlmDiscoverySubnetPrefix = null;

            Dispatcher.UIThread.RunJobs();

            // Should not crash
            Assert.Null(viewModel.LlmDiscoverySubnetPrefix);

            window.Close();
        }
    }
}
