using Asp.Versioning;
using CurrencyConverter.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyConverter.API.Controllers;

/// <summary>
/// RatesController provides endpoints for currency exchange rates operations.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[ApiVersion("1.0")]
public class RatesController : ControllerBase
{
    private readonly IMediator _mediator;

    private readonly ILogger<RatesController> _logger;

    public RatesController(IMediator mediator, ILogger<RatesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets the latest exchange rates for a specified base currency.
    /// </summary>
    /// <remarks> Requires User or Admin role. Currencies TRY, PLN, THB, MXN are not supported.</remarks>
    /// <param name="query"></param>
    /// <returns></returns>
    [HttpGet("latest")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetLatestRates([FromQuery] GetLatestRatesQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Converts an amount from one currency to another using the latest exchange rates.
    /// </summary>
    /// <remarks> Requires User or Admin role. Currencies TRY, PLN, THB, MXN are not supported.</remarks>
    /// <param name="query"></param>
    /// <returns></returns>
    [HttpGet("convert")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> Convert([FromQuery] ConvertCurrencyQuery query)
    {
        if (IsExcludedCurrency(query.FromCurrency) || IsExcludedCurrency(query.ToCurrency))
            return BadRequest("Currencies TRY, PLN, THB, MXN are not supported.");

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Gets historical exchange rates for a specified base currency within a date range.
    /// </summary>
    /// <remarks> Requires Admin role. </remarks>
    /// <param name="query"></param>
    /// <returns></returns>
    [HttpGet("historical")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetHistoricalRates([FromQuery] GetHistoricalRatesQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    private static bool IsExcludedCurrency(string currency) =>
        new[] { "TRY", "PLN", "THB", "MXN" }.Contains(currency?.ToUpper());
}