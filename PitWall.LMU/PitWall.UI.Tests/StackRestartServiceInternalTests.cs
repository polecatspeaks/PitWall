using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;
using PitWall.UI.Services;
using PitWall.UI.ViewModels;
using Xunit;

namespace PitWall.UI.Tests
{
    /// <summary>
    /// Tests for StackRestartService internal helpers, NullAgentConfigClient,
    /// NullStackRestartService, and SessionSummaryDto.
    /// </summary>
    public class StackRestartServiceInternalTests
    {
        #region FindSolutionRoot Tests

        [Fact]
        public void FindSolutionRoot_DirectoryWithSln_ReturnsPath()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "PitWallTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "PitWall.LMU.sln"), "");

            try
            {
                var result = StackRestartService.FindSolutionRoot(tempDir);

                Assert.Equal(tempDir, result);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindSolutionRoot_ChildDirectory_FindsParent()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "PitWallTest_" + Guid.NewGuid().ToString("N"));
            var childDir = Path.Combine(tempDir, "child", "grandchild");
            Directory.CreateDirectory(childDir);
            File.WriteAllText(Path.Combine(tempDir, "PitWall.LMU.sln"), "");

            try
            {
                var result = StackRestartService.FindSolutionRoot(childDir);

                Assert.Equal(tempDir, result);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindSolutionRoot_NoSlnAnywhere_ReturnsNull()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "PitWallNoSln_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var result = StackRestartService.FindSolutionRoot(tempDir);

                Assert.Null(result);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        #endregion

        #region ResolveBaseUri Tests

        [Fact]
        public void ResolveBaseUri_ValidUri_ReturnsAuthority()
        {
            var result = StackRestartService.ResolveBaseUri("http://192.168.1.100:5236", 5236);

            Assert.Equal("http://192.168.1.100:5236", result);
        }

        [Fact]
        public void ResolveBaseUri_NullEnvValue_ReturnsLocalhostDefault()
        {
            var result = StackRestartService.ResolveBaseUri(null, 5236);

            Assert.Equal("http://localhost:5236", result);
        }

        [Fact]
        public void ResolveBaseUri_EmptyEnvValue_ReturnsLocalhostDefault()
        {
            var result = StackRestartService.ResolveBaseUri("", 9999);

            Assert.Equal("http://localhost:9999", result);
        }

        [Fact]
        public void ResolveBaseUri_WhitespaceEnvValue_ReturnsLocalhostDefault()
        {
            var result = StackRestartService.ResolveBaseUri("   ", 5000);

            Assert.Equal("http://localhost:5000", result);
        }

        [Fact]
        public void ResolveBaseUri_InvalidUri_ReturnsLocalhostDefault()
        {
            var result = StackRestartService.ResolveBaseUri("not-a-valid-uri", 5236);

            Assert.Equal("http://localhost:5236", result);
        }

        [Fact]
        public void ResolveBaseUri_UriWithPath_StripsPathKeepsAuthority()
        {
            var result = StackRestartService.ResolveBaseUri("http://10.0.0.5:5139/api/health", 5139);

            Assert.Equal("http://10.0.0.5:5139", result);
        }

        [Fact]
        public void ResolveBaseUri_DifferentPorts_UsesProvidedDefault()
        {
            var result = StackRestartService.ResolveBaseUri(null, 5139);

            Assert.Equal("http://localhost:5139", result);
        }

        #endregion

        #region NullStackRestartService Tests

        [Fact]
        public void NullStackRestartService_Production_Restart_ReturnsFailure()
        {
            // Use FQN to disambiguate from test-assembly duplicate
            var service = new PitWall.UI.Services.NullStackRestartService();

            var result = service.Restart();

            Assert.False(result.Success);
            Assert.Contains("unavailable", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region NullAgentConfigClient Tests (Production)

        [Fact]
        public async Task NullAgentConfigClient_Production_GetConfigAsync_ReturnsDefault()
        {
            // Use FQN to disambiguate from test-assembly duplicate
            var client = new PitWall.UI.ViewModels.NullAgentConfigClient();

            var result = await client.GetConfigAsync(CancellationToken.None);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task NullAgentConfigClient_Production_UpdateConfigAsync_ReturnsDefault()
        {
            var client = new PitWall.UI.ViewModels.NullAgentConfigClient();
            var update = new AgentConfigUpdateDto { EnableLLM = true };

            var result = await client.UpdateConfigAsync(update, CancellationToken.None);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task NullAgentConfigClient_Production_DiscoverEndpointsAsync_ReturnsSampleEndpoints()
        {
            var client = new PitWall.UI.ViewModels.NullAgentConfigClient();

            var result = await client.DiscoverEndpointsAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.Count > 0);
        }

        [Fact]
        public async Task NullAgentConfigClient_Production_GetHealthAsync_ReturnsDefault()
        {
            var client = new PitWall.UI.ViewModels.NullAgentConfigClient();

            var result = await client.GetHealthAsync(CancellationToken.None);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task NullAgentConfigClient_Production_TestLlmAsync_ReturnsDefault()
        {
            var client = new PitWall.UI.ViewModels.NullAgentConfigClient();

            var result = await client.TestLlmAsync(CancellationToken.None);

            Assert.NotNull(result);
        }

        #endregion

        #region SessionSummaryDto Tests

        [Fact]
        public void SessionSummaryDto_DisplayName_WithStartTime_IncludesDate()
        {
            var dto = new SessionSummaryDto
            {
                SessionId = 42,
                StartTimeUtc = new DateTimeOffset(2025, 6, 15, 14, 30, 0, TimeSpan.Zero),
                Track = "Spa",
                Car = "Ferrari 499P"
            };

            var display = dto.DisplayName;

            Assert.Contains("42:", display);
            Assert.Contains("Spa", display);
            Assert.Contains("Ferrari 499P", display);
        }

        [Fact]
        public void SessionSummaryDto_DisplayName_NoStartTime_ShowsUnknownDate()
        {
            var dto = new SessionSummaryDto
            {
                SessionId = 1,
                StartTimeUtc = null,
                Track = "Monza",
                Car = "Porsche 963"
            };

            var display = dto.DisplayName;

            Assert.Contains("Unknown date", display);
            Assert.Contains("Monza", display);
            Assert.Contains("Porsche 963", display);
        }

        [Fact]
        public void SessionSummaryDto_Defaults_SetCorrectly()
        {
            var dto = new SessionSummaryDto();

            Assert.Equal("Unknown", dto.Track);
            Assert.Equal("Unknown", dto.Car);
            Assert.Null(dto.StartTimeUtc);
            Assert.Null(dto.EndTimeUtc);
            Assert.Null(dto.TrackId);
        }

        #endregion

        #region StackRestartResult Tests

        [Fact]
        public void StackRestartResult_RecordEquality()
        {
            var a = new StackRestartResult(true, "OK");
            var b = new StackRestartResult(true, "OK");

            Assert.Equal(a, b);
        }

        [Fact]
        public void StackRestartResult_RecordInequality()
        {
            var a = new StackRestartResult(true, "OK");
            var b = new StackRestartResult(false, "Failed");

            Assert.NotEqual(a, b);
        }

        #endregion
    }
}
