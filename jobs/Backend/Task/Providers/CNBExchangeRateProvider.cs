﻿using ExchangeRateUpdater.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ExchangeRateUpdater.Providers;

public class CNBExchangeRateProvider : IExchangeRateProvider
{
    /// <summary>
    /// Should return exchange rates among the specified currencies that are defined by the source. But only those defined
    /// by the source, do not return calculated exchange rates. E.g. if the source contains "CZK/USD" but not "USD/CZK",
    /// do not return exchange rate "USD/CZK" with value calculated as 1 / "CZK/USD". If the source does not provide
    /// some of the currencies, ignore them.
    /// </summary>

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CNBExchangeRateProvider> _logger;

    public CNBExchangeRateProvider(HttpClient httpClient, IConfiguration configuration, ILogger<CNBExchangeRateProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IEnumerable<ExchangeRate>> GetDailyExchangeRateAsync(string date) 
    {
        // TODO: verify input date format
        // TODO: throw on invalid dates? rates update around 14:30 daily, how to treat same day request
        // TODO: cache
        string baseUrl = _configuration["CNBApi:BaseUrl"] + _configuration["CNBApi:ExchangeRateEndpoint"];
        string defaultCurrency = _configuration["CNBApi:DefaultCurrency"];

        try
        {
            string apiUrl = $"{baseUrl}daily?date={date}&lang=EN"; // TODO: might move later

            HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();

                var apiData = JsonConvert.DeserializeObject<ExchangeRateApiData>(responseContent);

                var exchangeRateItems = apiData.Rates.Select(rateItem => new ExchangeRate(
                    new Currency(rateItem.CurrencyCode),
                    new Currency(defaultCurrency),
                    rateItem.Rate
                ));

                _logger.LogInformation("Fetched exchange rate succesfully");

                return exchangeRateItems;
            }
            else
            {
                _logger.LogError($"Failed to fetch exchange. Status code: {response.StatusCode}");
                throw new Exception("Fetch failed"); // TODO: improve later
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"An error occurred while fetching exchange rate {ex.Message}");
            throw;
        }
    }
}

public record ExchangeRateApiData
{
    public IEnumerable<ExchangeRateItem> Rates { get; set; }
}

public record ExchangeRateItem
{
    public string ValidFor { get; set; }
    public int Order { get; set; }
    public string Country { get; set; }
    public string Currency { get; set; }
    public int Amount { get; set; }
    public string CurrencyCode { get; set; }
    public decimal Rate { get; set; }
}
