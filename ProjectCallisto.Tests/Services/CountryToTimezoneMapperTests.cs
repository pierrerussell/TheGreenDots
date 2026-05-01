using FluentAssertions;
using Xunit;

namespace ProjectCallisto.Tests.Services;

/// <summary>
/// Tests the country to timezone mapping logic.
/// This replicates the MapCountryToTimezone method from OrganisationOnboardingService.
/// </summary>
public class CountryToTimezoneMapperTests
{
    [Theory]
    [InlineData("SG", "Asia/Singapore")]
    [InlineData("MY", "Asia/Kuala_Lumpur")]
    [InlineData("ID", "Asia/Jakarta")]
    public void MapCountryToTimezone_PrimaryMarkets_ReturnsCorrectTimezone(string country, string expectedTimezone)
    {
        // Act
        var result = MapCountryToTimezoneTestHelper(country);

        // Assert
        result.Should().Be(expectedTimezone);
    }

    [Theory]
    [InlineData("US", "America/New_York")]
    [InlineData("GB", "Europe/London")]
    [InlineData("AU", "Australia/Sydney")]
    [InlineData("JP", "Asia/Tokyo")]
    [InlineData("DE", "Europe/Berlin")]
    [InlineData("FR", "Europe/Paris")]
    [InlineData("CA", "America/Toronto")]
    [InlineData("IN", "Asia/Kolkata")]
    [InlineData("CN", "Asia/Shanghai")]
    [InlineData("BR", "America/Sao_Paulo")]
    public void MapCountryToTimezone_CommonCountries_ReturnsCorrectTimezone(string country, string expectedTimezone)
    {
        // Act
        var result = MapCountryToTimezoneTestHelper(country);

        // Assert
        result.Should().Be(expectedTimezone);
    }

    [Theory]
    [InlineData("MX", "America/Mexico_City")]
    [InlineData("NL", "Europe/Amsterdam")]
    [InlineData("SE", "Europe/Stockholm")]
    [InlineData("NO", "Europe/Oslo")]
    [InlineData("DK", "Europe/Copenhagen")]
    [InlineData("FI", "Europe/Helsinki")]
    [InlineData("IT", "Europe/Rome")]
    [InlineData("ES", "Europe/Madrid")]
    [InlineData("PT", "Europe/Lisbon")]
    [InlineData("PL", "Europe/Warsaw")]
    public void MapCountryToTimezone_EuropeanCountries_ReturnsCorrectTimezone(string country, string expectedTimezone)
    {
        // Act
        var result = MapCountryToTimezoneTestHelper(country);

        // Assert
        result.Should().Be(expectedTimezone);
    }

    [Theory]
    [InlineData("CZ", "Europe/Prague")]
    [InlineData("AT", "Europe/Vienna")]
    [InlineData("CH", "Europe/Zurich")]
    [InlineData("BE", "Europe/Brussels")]
    [InlineData("IE", "Europe/Dublin")]
    [InlineData("NZ", "Pacific/Auckland")]
    [InlineData("ZA", "Africa/Johannesburg")]
    public void MapCountryToTimezone_OtherCountries_ReturnsCorrectTimezone(string country, string expectedTimezone)
    {
        // Act
        var result = MapCountryToTimezoneTestHelper(country);

        // Assert
        result.Should().Be(expectedTimezone);
    }

    [Theory]
    [InlineData("AE", "Asia/Dubai")]
    [InlineData("SA", "Asia/Riyadh")]
    [InlineData("KR", "Asia/Seoul")]
    [InlineData("TH", "Asia/Bangkok")]
    [InlineData("PH", "Asia/Manila")]
    [InlineData("VN", "Asia/Ho_Chi_Minh")]
    [InlineData("HK", "Asia/Hong_Kong")]
    [InlineData("TW", "Asia/Taipei")]
    public void MapCountryToTimezone_AsianCountries_ReturnsCorrectTimezone(string country, string expectedTimezone)
    {
        // Act
        var result = MapCountryToTimezoneTestHelper(country);

        // Assert
        result.Should().Be(expectedTimezone);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("ZZ")]
    [InlineData("UNKNOWN")]
    public void MapCountryToTimezone_UnknownCountry_ReturnsFallbackUTC(string country)
    {
        // Act
        var result = MapCountryToTimezoneTestHelper(country);

        // Assert
        result.Should().Be("UTC");
    }

    [Theory]
    [InlineData("sg")]
    [InlineData("Sg")]
    [InlineData("SG")]
    public void MapCountryToTimezone_CaseInsensitive_WorksCorrectly(string country)
    {
        // Act
        var result = MapCountryToTimezoneTestHelper(country);

        // Assert
        result.Should().Be("Asia/Singapore");
    }

    [Fact]
    public void MapCountryToTimezone_AllMappedTimezonesAreValid()
    {
        // Arrange
        var countries = new[] {
            "SG", "MY", "ID", "US", "GB", "AU", "JP", "DE", "FR", "CA",
            "IN", "CN", "BR", "MX", "NL", "SE", "NO", "DK", "FI", "IT",
            "ES", "PT", "PL", "CZ", "AT", "CH", "BE", "IE", "NZ", "ZA",
            "AE", "SA", "KR", "TH", "PH", "VN", "HK", "TW"
        };

        // Act & Assert
        foreach (var country in countries)
        {
            var timezone = MapCountryToTimezoneTestHelper(country);
            var action = () => TimeZoneInfo.FindSystemTimeZoneById(timezone);
            action.Should().NotThrow($"timezone {timezone} for country {country} should be valid");
        }
    }

    // Helper method that replicates MapCountryToTimezone from OrganisationOnboardingService
    private static string MapCountryToTimezoneTestHelper(string countryCode)
    {
        return countryCode.ToUpperInvariant() switch
        {
            "SG" => "Asia/Singapore",
            "MY" => "Asia/Kuala_Lumpur",
            "ID" => "Asia/Jakarta",
            "US" => "America/New_York",
            "GB" => "Europe/London",
            "AU" => "Australia/Sydney",
            "JP" => "Asia/Tokyo",
            "DE" => "Europe/Berlin",
            "FR" => "Europe/Paris",
            "CA" => "America/Toronto",
            "IN" => "Asia/Kolkata",
            "CN" => "Asia/Shanghai",
            "BR" => "America/Sao_Paulo",
            "MX" => "America/Mexico_City",
            "NL" => "Europe/Amsterdam",
            "SE" => "Europe/Stockholm",
            "NO" => "Europe/Oslo",
            "DK" => "Europe/Copenhagen",
            "FI" => "Europe/Helsinki",
            "IT" => "Europe/Rome",
            "ES" => "Europe/Madrid",
            "PT" => "Europe/Lisbon",
            "PL" => "Europe/Warsaw",
            "CZ" => "Europe/Prague",
            "AT" => "Europe/Vienna",
            "CH" => "Europe/Zurich",
            "BE" => "Europe/Brussels",
            "IE" => "Europe/Dublin",
            "NZ" => "Pacific/Auckland",
            "ZA" => "Africa/Johannesburg",
            "AE" => "Asia/Dubai",
            "SA" => "Asia/Riyadh",
            "KR" => "Asia/Seoul",
            "TH" => "Asia/Bangkok",
            "PH" => "Asia/Manila",
            "VN" => "Asia/Ho_Chi_Minh",
            "HK" => "Asia/Hong_Kong",
            "TW" => "Asia/Taipei",
            _ => "UTC"
        };
    }
}
