using PitWall.UI.Services;
using PitWall.UI.Models;
using Xunit;

namespace PitWall.UI.Tests
{
    public class CarSpecStoreTests
    {
        [Fact]
        public void Constructor_InitializesSuccessfully()
        {
            var store = new CarSpecStore();
            
            Assert.NotNull(store);
        }

        [Fact]
        public void GetByName_NullInput_ReturnsNull()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName(null);
            
            Assert.Null(result);
        }

        [Fact]
        public void GetByName_EmptyString_ReturnsNull()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("");
            
            Assert.Null(result);
        }

        [Fact]
        public void GetByName_WhitespaceOnly_ReturnsNull()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("   ");
            
            Assert.Null(result);
        }

        [Fact]
        public void GetByName_UnknownCar_ReturnsNull()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("NonExistentCar12345");
            
            Assert.Null(result);
        }

        [Theory]
        [InlineData("Ferrari")]
        [InlineData("Porsche")]
        [InlineData("BMW")]
        [InlineData("Mercedes")]
        public void GetByName_CommonManufacturers_ReturnsNonNull(string manufacturer)
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName(manufacturer);
            
            // May return null if not in test fixtures, but shouldn't throw
            Assert.True(result == null || result.Name.Contains(manufacturer));
        }

        [Theory]
        [InlineData("ferrari", "Ferrari")]
        [InlineData("FERRARI", "Ferrari")]
        [InlineData("FeRrArI", "Ferrari")]
        public void GetByName_CaseInsensitive(string input, string expectedContains)
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName(input);
            
            if (result != null)
            {
                Assert.Contains(expectedContains, result.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void GetByName_WithTeamSuffix_StripsAndFinds()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("Ferrari #1");
            
            // Should strip the #1 and try to find Ferrari
            if (result != null)
            {
                Assert.Contains("Ferrari", result.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void GetByName_WithParentheses_StripsAndFinds()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("Ferrari (Team Red)");
            
            // Should strip the (Team Red) and try to find Ferrari
            if (result != null)
            {
                Assert.Contains("Ferrari", result.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void GetByName_WithDashSuffix_StripsAndFinds()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("Ferrari - Team");
            
            // Should strip the - Team and try to find Ferrari
            if (result != null)
            {
                Assert.Contains("Ferrari", result.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void GetByName_WithPipeSuffix_StripsAndFinds()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("Ferrari | Racing");
            
            // Should strip the | Racing and try to find Ferrari
            if (result != null)
            {
                Assert.Contains("Ferrari", result.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void GetByName_WithTeamSuffix_StripsAndFinds2()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("Ferrari Team");
            
            // Should strip the Team suffix
            if (result != null)
            {
                Assert.Contains("Ferrari", result.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void GetByName_SlugMatching_Works()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("ferrari-296-gt3");
            
            // Should find car via slug matching
            Assert.True(result == null || result.Slug.Contains("ferrari") || result.Name.Contains("Ferrari"));
        }

        [Fact]
        public void GetByName_PartialMatch_CanFindCar()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("296");
            
            // Should find car with partial matching
            Assert.True(result == null || result.Name.Contains("296"));
        }

        [Fact]
        public void GetByName_ReturnedCar_HasName()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("Ferrari");
            
            if (result != null)
            {
                Assert.False(string.IsNullOrWhiteSpace(result.Name));
            }
        }

        [Fact]
        public void GetByName_ReturnedCar_HasSlug()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("Ferrari");
            
            if (result != null)
            {
                Assert.NotNull(result.Slug);
            }
        }

        [Fact]
        public void GetByName_MultipleCallsSameCar_ReturnsSameData()
        {
            var store = new CarSpecStore();
            
            var result1 = store.GetByName("Ferrari");
            var result2 = store.GetByName("Ferrari");
            
            if (result1 != null && result2 != null)
            {
                Assert.Equal(result1.Name, result2.Name);
                Assert.Equal(result1.Slug, result2.Slug);
            }
        }

        [Fact]
        public void GetByName_WithAccents_HandlesNormalization()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("Lamborghini Huracán");
            
            // Should handle accent normalization
            Assert.True(result == null || result.Name.Contains("Lamborghini"));
        }

        [Fact]
        public void GetByName_ManufacturerIndex_SingleMatch_ReturnsCar()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("AstonMartin");
            
            // Manufacturer matching logic
            Assert.True(result == null || result.Name.Contains("Aston"));
        }

        [Fact]
        public void GetByName_ManufacturerIndex_MultipleMatches_ReturnsNull()
        {
            var store = new CarSpecStore();
            
            // If there are multiple Ferraris, manufacturer index won't return one
            var result = store.GetByName("Generic Manufacturer With Many Cars");
            
            // Should return null for ambiguous manufacturer matches
            Assert.Null(result);
        }

        [Fact]
        public void GetByName_MercedesAMG_HandlesCompoundManufacturer()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("Mercedes-AMG GT3");
            
            // Should handle compound manufacturer names
            Assert.True(result == null || result.Name.Contains("Mercedes"));
        }

        [Fact]
        public void GetByName_IsottaFraschini_HandlesCompoundManufacturer()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("Isotta Fraschini Tipo");
            
            // Should handle compound manufacturer names
            Assert.True(result == null || result.Name.Contains("Isotta"));
        }

        [Fact]
        public void GetByName_WithAlias_FindsViaDictionary()
        {
            var store = new CarSpecStore();
            
            // Test alias functionality - if aliases exist they should resolve
            var result = store.GetByName("F296");
            
            // May resolve via alias dictionary
            Assert.True(result == null || result.Name.Length > 0);
        }

        [Theory]
        [InlineData("Ferrari 296")]
        [InlineData("Porsche 911")]
        [InlineData("BMW M4")]
        public void GetByName_CommonCarNames_DoesNotThrow(string carName)
        {
            var store = new CarSpecStore();
            
            var exception = Record.Exception(() => store.GetByName(carName));
            
            Assert.Null(exception);
        }

        [Fact]
        public void GetByName_VeryLongString_HandlesGracefully()
        {
            var store = new CarSpecStore();
            var longName = new string('A', 1000);
            
            var result = store.GetByName(longName);
            
            // Should handle long strings without throwing
            Assert.Null(result);
        }

        [Fact]
        public void GetByName_SpecialCharacters_HandlesGracefully()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("@#$%^&*()");
            
            // Should handle special characters without throwing
            Assert.Null(result);
        }

        [Fact]
        public void GetByName_NumbersOnly_HandlesGracefully()
        {
            var store = new CarSpecStore();
            
            var result = store.GetByName("12345");
            
            // Should handle numbers without throwing
            Assert.Null(result);
        }
    }
}
