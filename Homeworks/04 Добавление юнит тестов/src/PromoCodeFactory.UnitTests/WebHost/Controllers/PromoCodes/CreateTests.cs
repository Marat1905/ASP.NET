using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PromoCodeFactory.Core.Abstractions.Repositories;
using PromoCodeFactory.Core.Domain.Administration;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.WebHost.Controllers;
using PromoCodeFactory.WebHost.Models.PromoCodes;
using Soenneker.Utils.AutoBogus;

namespace PromoCodeFactory.UnitTests.WebHost.Controllers.PromoCodes;

public class CreateTests
{
    private readonly Mock<IRepository<PromoCode>> _promoCodesRepositoryMock;
    private readonly Mock<IRepository<Customer>> _customersRepositoryMock;
    private readonly Mock<IRepository<CustomerPromoCode>> _customerPromoCodesRepositoryMock;
    private readonly Mock<IRepository<Partner>> _partnersRepositoryMock;
    private readonly Mock<IRepository<Preference>> _preferencesRepositoryMock;
    private readonly PromoCodesController _sut;

    public CreateTests()
    {
        _promoCodesRepositoryMock = new Mock<IRepository<PromoCode>>();
        _customersRepositoryMock = new Mock<IRepository<Customer>>();
        _customerPromoCodesRepositoryMock = new Mock<IRepository<CustomerPromoCode>>();
        _partnersRepositoryMock = new Mock<IRepository<Partner>>();
        _preferencesRepositoryMock = new Mock<IRepository<Preference>>();
        _sut = new PromoCodesController(
            _promoCodesRepositoryMock.Object,
            _customersRepositoryMock.Object,
            _customerPromoCodesRepositoryMock.Object,
            _partnersRepositoryMock.Object,
            _preferencesRepositoryMock.Object);
    }

    /// <summary>
    /// Проверить, что если партнер не найден, то возвращается 404 с корректно заполненным ProblemDetails
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Create_WhenPartnerNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new AutoFaker<PromoCodeCreateRequest>().Generate();

        _partnersRepositoryMock
            .Setup(r => r.GetById(request.PartnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Partner?)null);

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result.Result;
        notFoundResult.Value.Should().BeOfType<ProblemDetails>();
        var problemDetails = (ProblemDetails)notFoundResult.Value!;
        problemDetails.Title.Should().Be("Partner not found");
    }

    /// <summary>
    /// Проверить, что если предпочтение не найдено, то возвращается 404 с корректно заполненным ProblemDetails
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Create_WhenPreferenceNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new AutoFaker<PromoCodeCreateRequest>().Generate();
        var partner = CreatePartner(isActive: true, withActiveLimit: true);

        _partnersRepositoryMock
            .Setup(r => r.GetById(request.PartnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _preferencesRepositoryMock
            .Setup(r => r.GetById(request.PreferenceId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Preference?)null);

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result.Result;
        notFoundResult.Value.Should().BeOfType<ProblemDetails>();
        var problemDetails = (ProblemDetails)notFoundResult.Value!;
        problemDetails.Title.Should().Be("Preference not found");
    }

    /// <summary>
    /// Проверить, что если нет активного лимита, то возвращается 422 с корректно заполненным ProblemDetails
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Create_WhenNoActiveLimit_ReturnsUnprocessableEntity()
    {
        // Arrange
        var request = new AutoFaker<PromoCodeCreateRequest>().Generate();
        var partner = CreatePartner(isActive: true, withActiveLimit: false); // нет активного лимита
        var preference = CreatePreference();

        _partnersRepositoryMock
            .Setup(r => r.GetById(request.PartnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _preferencesRepositoryMock
            .Setup(r => r.GetById(request.PreferenceId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preference);

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result.Result;
        objectResult.StatusCode.Should().Be(422);
        objectResult.Value.Should().BeOfType<ProblemDetails>();
        var problemDetails = (ProblemDetails)objectResult.Value!;
        problemDetails.Title.Should().Be("No active limit");
    }

    /// <summary>
    /// Проверить, что если IssuedCount >= Limit, то возвращается 422 с корректно заполненным ProblemDetails
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Create_WhenLimitExceeded_ReturnsUnprocessableEntity()
    {
        // Arrange
        var request = new AutoFaker<PromoCodeCreateRequest>().Generate();
        var activeLimit = CreateLimit(limit: 10, issuedCount: 10); // лимит исчерпан
        var partner = CreatePartner(isActive: true, activeLimit: activeLimit);
        var preference = CreatePreference();

        _partnersRepositoryMock
            .Setup(r => r.GetById(request.PartnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _preferencesRepositoryMock
            .Setup(r => r.GetById(request.PreferenceId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preference);

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result.Result;
        objectResult.StatusCode.Should().Be(422);
        objectResult.Value.Should().BeOfType<ProblemDetails>();
        var problemDetails = (ProblemDetails)objectResult.Value!;
        problemDetails.Title.Should().Be("Limit exceeded");
    }

    /// <summary>
    /// Create_WhenValidRequest_ReturnsCreatedAndIncrementsIssuedCount: Проверить, что промокод создается и у лимита увеличивается IssuedCount
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Create_WhenValidRequest_ReturnsCreatedAndIncrementsIssuedCount()
    {
        // Arrange
        var request = new AutoFaker<PromoCodeCreateRequest>()
            .RuleFor(r => r.BeginDate, DateTimeOffset.UtcNow)
            .RuleFor(r => r.EndDate, DateTimeOffset.UtcNow.AddDays(30))
            .Generate();
        var initialIssuedCount = 5;
        var activeLimit = CreateLimit(limit: 100, issuedCount: initialIssuedCount);
        var partner = CreatePartner(isActive: true, activeLimit: activeLimit);
        var preference = CreatePreference();
        var customers = new AutoFaker<Customer>()
            .RuleFor(c => c.Id, _ => Guid.NewGuid())
            .Generate(3);

        _partnersRepositoryMock
            .Setup(r => r.GetById(request.PartnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _preferencesRepositoryMock
            .Setup(r => r.GetById(request.PreferenceId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preference);

        _customersRepositoryMock
            .Setup(r => r.GetWhere(
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, bool>>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(customers);

        PromoCode? addedPromoCode = null;
        _promoCodesRepositoryMock
            .Setup(r => r.Add(It.IsAny<PromoCode>(), It.IsAny<CancellationToken>()))
            .Callback<PromoCode, CancellationToken>((pc, _) => addedPromoCode = pc)
            .Returns(Task.CompletedTask);

        Partner? updatedPartner = null;
        _partnersRepositoryMock
            .Setup(r => r.Update(It.IsAny<Partner>(), It.IsAny<CancellationToken>()))
            .Callback<Partner, CancellationToken>((p, _) => updatedPartner = p)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdAtResult = (CreatedAtActionResult)result.Result;
        createdAtResult.ActionName.Should().Be("GetById");
        createdAtResult.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(addedPromoCode?.Id);

        // Проверяем, что промокод создан корректно
        addedPromoCode.Should().NotBeNull();
        addedPromoCode!.Code.Should().Be(request.Code);
        addedPromoCode.ServiceInfo.Should().Be(request.ServiceInfo);
        addedPromoCode.Partner.Should().Be(partner);
        addedPromoCode.Preference.Should().Be(preference);
        addedPromoCode.CustomerPromoCodes.Should().HaveCount(customers.Count);

        // Проверяем, что IssuedCount увеличился
        _partnersRepositoryMock.Verify(r => r.Update(It.IsAny<Partner>(), It.IsAny<CancellationToken>()), Times.Once);
        activeLimit.IssuedCount.Should().Be(initialIssuedCount + 1);
        updatedPartner.Should().BeSameAs(partner);
    }

    private static Partner CreatePartner(bool isActive, bool withActiveLimit = true, PartnerPromoCodeLimit? activeLimit = null)
    {
        var role = new AutoFaker<Role>()
            .RuleFor(r => r.Id, _ => Guid.NewGuid())
            .Generate();

        var employee = new AutoFaker<Employee>()
            .RuleFor(e => e.Id, _ => Guid.NewGuid())
            .RuleFor(e => e.Role, role)
            .Generate();

        var limits = new List<PartnerPromoCodeLimit>();
        if (withActiveLimit)
        {
            var limit = activeLimit ?? CreateLimit(limit: 100, issuedCount: 0);
            limits.Add(limit);
        }

        return new AutoFaker<Partner>()
            .RuleFor(p => p.Id, _ => Guid.NewGuid())
            .RuleFor(p => p.IsActive, _ => isActive)
            .RuleFor(p => p.Manager, employee)
            .RuleFor(p => p.PartnerLimits, limits)
            .Generate();
    }

    private static PartnerPromoCodeLimit CreateLimit(int limit, int issuedCount)
    {
        return new AutoFaker<PartnerPromoCodeLimit>()
            .RuleFor(l => l.Id, _ => Guid.NewGuid())
            .RuleFor(l => l.CanceledAt, _ => null)
            .RuleFor(l => l.CreatedAt, _ => DateTimeOffset.UtcNow.AddDays(-5))
            .RuleFor(l => l.EndAt, _ => DateTimeOffset.UtcNow.AddDays(30))
            .RuleFor(l => l.Limit, _ => limit)
            .RuleFor(l => l.IssuedCount, _ => issuedCount)
            .Generate();
    }

    private static Preference CreatePreference()
    {
        return new AutoFaker<Preference>()
            .RuleFor(p => p.Id, _ => Guid.NewGuid())
            .RuleFor(p => p.Name, _ => "Test Preference")
            .Generate();
    }
}
