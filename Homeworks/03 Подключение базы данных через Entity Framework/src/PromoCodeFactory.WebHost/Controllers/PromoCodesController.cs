using Microsoft.AspNetCore.Mvc;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.WebHost.Mapping;
using PromoCodeFactory.WebHost.Models.PromoCodes;

namespace PromoCodeFactory.WebHost.Controllers;

/// <summary>
/// Промокоды
/// </summary>
public class PromoCodesController(
    IRepository<PromoCode> promoCodeRepository,
    IRepository<Employee> employeeRepository,
    IRepository<Preference> preferenceRepository,
    IRepository<Customer> customerRepository,
    IRepository<CustomerPromoCode> customerPromoCodeRepository) : BaseController
{
    /// <summary>
    /// Получить все промокоды
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PromoCodeShortResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PromoCodeShortResponse>>> Get(CancellationToken ct)
    {
        var promoCodes = await promoCodeRepository.GetAll(withIncludes: true, ct);
        return Ok(promoCodes.Select(PromoCodesMapper.ToPromoCodeShortResponse).ToList());
    }


    /// <summary>
    /// Получить промокод по id
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PromoCodeShortResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromoCodeShortResponse>> GetById(Guid id, CancellationToken ct)
    {
        var promoCode = await promoCodeRepository.GetById(id, withIncludes: true, ct);
        return promoCode is null ? NotFound() : Ok(PromoCodesMapper.ToPromoCodeShortResponse(promoCode));
    }

    /// <summary>
    /// Создать промокод и выдать его клиентам с указанным предпочтением
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PromoCodeShortResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PromoCodeShortResponse>> Create([FromBody] PromoCodeCreateRequest request, CancellationToken ct)
    {
        var partnerManager = await employeeRepository.GetById(request.PartnerManagerId, ct: ct);
        if (partnerManager is null)
            return BadRequest(new ProblemDetails { Title = "Invalid partner manager", Detail = $"Employee with Id {request.PartnerManagerId} not found." });

        var preference = await preferenceRepository.GetById(request.PreferenceId, ct: ct);
        if (preference is null)
            return BadRequest(new ProblemDetails { Title = "Invalid preference", Detail = $"Preference with Id {request.PreferenceId} not found." });

        var promoCode = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            ServiceInfo = request.ServiceInfo,
            PartnerName = request.PartnerName,
            BeginDate = request.BeginDate,
            EndDate = request.EndDate,
            PartnerManager = partnerManager,
            Preference = preference
        };
        await promoCodeRepository.Add(promoCode, ct);

        var customersWithPreference = await customerRepository.GetWhere(c => c.Preferences.Contains(preference), withIncludes: false, ct);
        foreach (var customer in customersWithPreference)
        {
            var customerPromoCode = new CustomerPromoCode
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                PromoCodeId = promoCode.Id,
                CreatedAt = DateTimeOffset.Now,
                AppliedAt = null
            };
            await customerPromoCodeRepository.Add(customerPromoCode, ct);
        }

        var created = await promoCodeRepository.GetById(promoCode.Id, withIncludes: true, ct);
        return CreatedAtAction(nameof(GetById), new { id = created!.Id }, PromoCodesMapper.ToPromoCodeShortResponse(created));
    }

    /// <summary>
    /// Применить промокод (отметить, что клиент использовал промокод)
    /// </summary>
    [HttpPost("{id:guid}/apply")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Apply([FromRoute] Guid id, [FromBody] PromoCodeApplyRequest request, CancellationToken ct)
    {
        var cpcList = await customerPromoCodeRepository.GetWhere(cpc => cpc.PromoCodeId == id && cpc.CustomerId == request.CustomerId, ct: ct);
        var cpc = cpcList.FirstOrDefault();
        if (cpc is null)
            return NotFound(new ProblemDetails { Title = "Not found", Detail = $"PromoCode {id} not assigned to customer {request.CustomerId}." });

        if (cpc.AppliedAt.HasValue)
            return BadRequest(new ProblemDetails { Title = "Already applied", Detail = "This promo code has already been applied by this customer." });

        cpc.AppliedAt = DateTimeOffset.Now;
        await customerPromoCodeRepository.Update(cpc, ct);
        return NoContent();
    }
}
