using Microsoft.AspNetCore.Mvc;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.WebHost.Mapping;
using PromoCodeFactory.WebHost.Models.Customers;

namespace PromoCodeFactory.WebHost.Controllers;

/// <summary>
/// Клиенты
/// </summary>
public class CustomersController(
    IRepository<Customer> customerRepository,
    IRepository<Preference> preferenceRepository,
    IRepository<PromoCode> promoCodeRepository) : BaseController
{
    /// <summary>
    /// Получить данные всех клиентов
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CustomerShortResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CustomerShortResponse>>> Get(CancellationToken ct)
    {
        var customers = await customerRepository.GetAll(withIncludes: true, ct);
        return Ok(customers.Select(CustomersMapper.ToCustomerShortResponse).ToList());
    }

    /// <summary>
    /// Получить данные клиента по Id
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid id, CancellationToken ct)
    {
        var customer = await customerRepository.GetById(id, withIncludes: true, ct);
        if (customer is null) return NotFound();

        var promoCodeIds = customer.CustomerPromoCodes.Select(cpc => cpc.PromoCodeId).Distinct().ToList();
        var promoCodes = promoCodeIds.Any()
            ? await promoCodeRepository.GetByRangeId(promoCodeIds, withIncludes: true, ct)
            : Array.Empty<PromoCode>();

        return Ok(CustomersMapper.ToCustomerResponse(customer, promoCodes));
    }

    /// <summary>
    /// Создать клиента
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerShortResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerShortResponse>> Create([FromBody] CustomerCreateRequest request, CancellationToken ct)
    {
        var preferences = await preferenceRepository.GetByRangeId(request.PreferenceIds, ct: ct);
        if (preferences.Count != request.PreferenceIds.Length)
        {
            var missing = request.PreferenceIds.Except(preferences.Select(p => p.Id));
            return BadRequest(new ProblemDetails { Title = "Invalid preferences", Detail = $"Preferences with ids {string.Join(",", missing)} not found." });
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Preferences = preferences.ToList()
        };
        await customerRepository.Add(customer, ct);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, CustomersMapper.ToCustomerShortResponse(customer));
    }

    /// <summary>
    /// Обновить клиента
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerShortResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerShortResponse>> Update(
        [FromRoute] Guid id,
        [FromBody] CustomerUpdateRequest request,
        CancellationToken ct)
    {
        var customer = await customerRepository.GetById(id, withIncludes: true, ct);
        if (customer is null) return NotFound();

        var preferences = await preferenceRepository.GetByRangeId(request.PreferenceIds, ct: ct);
        if (preferences.Count != request.PreferenceIds.Length)
        {
            var missing = request.PreferenceIds.Except(preferences.Select(p => p.Id));
            return BadRequest(new ProblemDetails { Title = "Invalid preferences", Detail = $"Preferences with ids {string.Join(",", missing)} not found." });
        }

        customer.FirstName = request.FirstName;
        customer.LastName = request.LastName;
        customer.Email = request.Email;
        customer.Preferences.Clear();
        foreach (var pref in preferences) customer.Preferences.Add(pref);

        await customerRepository.Update(customer, ct);
        return Ok(CustomersMapper.ToCustomerShortResponse(customer));
    }

    /// <summary>
    /// Удалить клиента
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await customerRepository.Delete(id, ct);
            return NoContent();
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }
}
