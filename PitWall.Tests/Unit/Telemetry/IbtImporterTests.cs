using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using PitWall.Telemetry;

namespace PitWall.Tests.Unit.Telemetry
{
    /// <summary>
    /// TDD RED Phase: Tests for IBT file importer
    /// All tests should FAIL initially (IbtImporter doesn't exist yet)
    /// 
    /// Test strategy:
    /// 1. GetTelemetryFolder - Test folder detection with override
    /// 2. ScanTelemetryFolder - Test file scanning
    /// 3. ImportIBTFile - Test complete import workflow
    /// </summary>
    public class IbtImporterTests
    {
        [Fact]
        public async Task GetTelemetryFolder_WhenDefaultPathExists_ReturnsPath()
        {
            // Arrange
            var importer = new IbtImporter();

            // Act
            var path = await importer.GetTelemetryFolderAsync();

            // Assert
            Assert.NotNull(path);
            Assert.NotEmpty(path);
            Assert.True(Directory.Exists(path) || path.Contains("telemetry"));
        }

        [Fact]
        public async Task ScanTelemetryFolder_WhenFolderHasFiles_ReturnsFileList()
        {
            // Arrange
            var importer = new IbtImporter();
            var testFolder = Path.GetTempPath();

            // Act
            var files = await importer.ScanTelemetryFolderAsync(testFolder);

            // Assert
            Assert.NotNull(files);
            Assert.IsType<List<IBTFileInfo>>(files);
        }

        [Fact]
        public async Task ImportIBTFile_WhenFileMissing_ThrowsException()
        {
            // Arrange
            var importer = new IbtImporter();
            var testFile = "nonexistent.ibt";

            // Act & Assert - Should throw because implementation doesn't exist
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await importer.ImportIBTFileAsync(testFile);
            });
        }

        [Fact]
        public void IbtFileReader_WhenValidFile_ReadsSessionInfo()
        {
            // Arrange
            string ibtPath = @"C:\Users\ohzee\Documents\iRacing\telemetry\mclaren720sgt3_charlotte 2025 roval2025 2025-11-16 13-15-19.ibt";
            
            // Skip test if file doesn't exist (for CI/CD)
            if (!File.Exists(ibtPath))
            {
                return;
            }

            // Act
            using var reader = new IbtFileReader(ibtPath);
            string yaml = reader.ReadSessionInfoYaml();
            var sessionInfo = reader.ParseSessionInfo();

            // Assert
            Assert.NotNull(yaml);
            Assert.NotEmpty(yaml);
            Assert.NotNull(sessionInfo);
            Assert.NotEmpty(sessionInfo);
        }

        [Fact]
        public void IbtFileReader_WhenValidFile_ReadsVariableHeaders()
        {
            // Arrange
            string ibtPath = @"C:\Users\ohzee\Documents\iRacing\telemetry\mclaren720sgt3_charlotte 2025 roval2025 2025-11-16 13-15-19.ibt";
            
            // Skip test if file doesn't exist
            if (!File.Exists(ibtPath))
            {
                return;
            }

            // Act
            using var reader = new IbtFileReader(ibtPath);
            var variables = reader.ReadVariableHeaders();

            // Assert
            Assert.NotNull(variables);
            Assert.NotEmpty(variables);
            
            // Should have standard telemetry variables
            Assert.Contains(variables, v => v.Name.Contains("Speed"));
            Assert.Contains(variables, v => v.Name.Contains("RPM") || v.Name.Contains("Rpm"));
            Assert.Contains(variables, v => v.Name.Contains("Fuel"));
        }

        // TODO: Add test for 60Hz sample extraction once we have mock IBT file
        // [Fact]
        // public async Task ImportIBTFile_WhenValid_Returns60HzSamples()

        // TODO: Add test for lap aggregation once we have mock IBT file  
        // [Fact]
        // public async Task ImportIBTFile_WhenValid_CalculatesLapMetadata()
    }
}
