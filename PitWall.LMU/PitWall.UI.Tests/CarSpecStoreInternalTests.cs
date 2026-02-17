using PitWall.UI.Services;
using Xunit;

namespace PitWall.UI.Tests
{
    /// <summary>
    /// Tests for CarSpecStore internal pure helper methods.
    /// </summary>
    public class CarSpecStoreInternalTests
    {
        #region NormalizeKey Tests

        [Fact]
        public void NormalizeKey_RemovesAccents()
        {
            var result = CarSpecStore.NormalizeKey("Pérez");

            Assert.Equal("Perez", result);
        }

        [Fact]
        public void NormalizeKey_PreservesNormal()
        {
            var result = CarSpecStore.NormalizeKey("Ferrari 499P");

            Assert.Equal("Ferrari 499P", result);
        }

        [Fact]
        public void NormalizeKey_TrimsWhitespace()
        {
            var result = CarSpecStore.NormalizeKey("  Porsche  ");

            Assert.Equal("Porsche", result);
        }

        [Fact]
        public void NormalizeKey_MultipleAccents()
        {
            var result = CarSpecStore.NormalizeKey("São Paülö");

            Assert.Equal("Sao Paulo", result);
        }

        #endregion

        #region ToSlug Tests

        [Fact]
        public void ToSlug_BasicName_ReturnsSlug()
        {
            var result = CarSpecStore.ToSlug("Ferrari 499P");

            Assert.Equal("ferrari-499p", result);
        }

        [Fact]
        public void ToSlug_SpecialChars_Dashes()
        {
            var result = CarSpecStore.ToSlug("Porsche 963 (2024)");

            Assert.Equal("porsche-963-2024", result);
        }

        [Fact]
        public void ToSlug_EmptyString_ReturnsEmpty()
        {
            var result = CarSpecStore.ToSlug("");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ToSlug_LeadingTrailingSpecial_TrimsDashes()
        {
            var result = CarSpecStore.ToSlug("--Ferrari--");

            Assert.Equal("ferrari", result);
        }

        [Fact]
        public void ToSlug_MultipleSeparators_Collapses()
        {
            var result = CarSpecStore.ToSlug("Car   Name");

            Assert.Equal("car-name", result);
        }

        #endregion

        #region StripTeamSuffix Tests

        [Fact]
        public void StripTeamSuffix_HashNumber_Stripped()
        {
            var result = CarSpecStore.StripTeamSuffix("Ferrari 499P #51");

            Assert.Equal("Ferrari 499P", result);
        }

        [Fact]
        public void StripTeamSuffix_Parentheses_Stripped()
        {
            var result = CarSpecStore.StripTeamSuffix("Porsche 963 (AF Corse)");

            Assert.Equal("Porsche 963", result);
        }

        [Fact]
        public void StripTeamSuffix_Dash_Stripped()
        {
            var result = CarSpecStore.StripTeamSuffix("BMW M4 GT3 - RLL Racing");

            Assert.Equal("BMW M4 GT3", result);
        }

        [Fact]
        public void StripTeamSuffix_Pipe_Stripped()
        {
            var result = CarSpecStore.StripTeamSuffix("Lamborghini SC63 | Iron Lynx");

            Assert.Equal("Lamborghini SC63", result);
        }

        [Fact]
        public void StripTeamSuffix_TeamKeyword_Stripped()
        {
            var result = CarSpecStore.StripTeamSuffix("Mercedes-AMG GT3 Team Strakka");

            Assert.Equal("Mercedes-AMG GT3", result);
        }

        [Fact]
        public void StripTeamSuffix_NoSuffix_ReturnsOriginal()
        {
            var result = CarSpecStore.StripTeamSuffix("Porsche 911");

            Assert.Equal("Porsche 911", result);
        }

        [Fact]
        public void StripTeamSuffix_Empty_ReturnsEmpty()
        {
            var result = CarSpecStore.StripTeamSuffix("");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void StripTeamSuffix_Whitespace_ReturnsEmpty()
        {
            var result = CarSpecStore.StripTeamSuffix("   ");

            Assert.Equal(string.Empty, result);
        }

        #endregion

        #region GetManufacturerKey Tests

        [Fact]
        public void GetManufacturerKey_AstonMartin_ReturnsFullPrefix()
        {
            var result = CarSpecStore.GetManufacturerKey("Aston Martin Vantage GT3");

            Assert.Equal("Aston Martin", result);
        }

        [Fact]
        public void GetManufacturerKey_IsottaFraschini_ReturnsFullPrefix()
        {
            var result = CarSpecStore.GetManufacturerKey("Isotta Fraschini Tipo 6 LMH");

            Assert.Equal("Isotta Fraschini", result);
        }

        [Fact]
        public void GetManufacturerKey_MercedesAmg_ReturnsFullPrefix()
        {
            var result = CarSpecStore.GetManufacturerKey("Mercedes-AMG GT3 2024");

            Assert.Equal("Mercedes-AMG", result);
        }

        [Fact]
        public void GetManufacturerKey_SingleWord_ReturnsFullName()
        {
            var result = CarSpecStore.GetManufacturerKey("Ferrari");

            Assert.Equal("Ferrari", result);
        }

        [Fact]
        public void GetManufacturerKey_TwoWords_ReturnsFirst()
        {
            var result = CarSpecStore.GetManufacturerKey("Ferrari 499P");

            Assert.Equal("Ferrari", result);
        }

        [Fact]
        public void GetManufacturerKey_Empty_ReturnsEmpty()
        {
            var result = CarSpecStore.GetManufacturerKey("");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetManufacturerKey_Whitespace_ReturnsEmpty()
        {
            var result = CarSpecStore.GetManufacturerKey("   ");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetManufacturerKey_Porsche_ReturnsFirst()
        {
            var result = CarSpecStore.GetManufacturerKey("Porsche 963 LMDh");

            Assert.Equal("Porsche", result);
        }

        #endregion
    }
}
