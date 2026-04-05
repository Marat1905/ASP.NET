using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PromoCodeFactory.Core.Abstractions.Repositories;
using PromoCodeFactory.Core.Domain.Administration;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.Core.Exceptions;
using PromoCodeFactory.WebHost.Controllers;
using PromoCodeFactory.WebHost.Models.Partners;
using Soenneker.Utils.AutoBogus;

namespace PromoCodeFactory.UnitTests.WebHost.Controllers.Partners;

public class SetLimitTests
{
    private readonly Mock<IRepository<Partner>> _partnersRepositoryMock;
    private readonly Mock<IRepository<PartnerPromoCodeLimit>> _partnerLimitsRepositoryMock;
    private readonly PartnersController _sut;

    public SetLimitTests()
    {
        _partnersRepositoryMock = new Mock<IRepository<Partner>>();
        _partnerLimitsRepositoryMock = new Mock<IRepository<PartnerPromoCodeLimit>>();
        _sut = new PartnersController(_partnersRepositoryMock.Object, _partnerLimitsRepositoryMock.Object);
    }

    /// <summary>
    /// Проверить, что если партнер не найден, то возвращается 404 с корректно заполненным ProblemDetails
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CreateLimit_WhenPartnerNotFound_ReturnsNotFound()
    {
        // Arrange
        var partnerId = Guid.NewGuid();
        var request = new AutoFaker<PartnerPromoCodeLimitCreateRequest>().Generate();

        _partnersRepositoryMock
            .Setup(r => r.GetById(partnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Partner?)null);

        // Act
        var result = await _sut.CreateLimit(partnerId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result.Result;
        notFoundResult.Value.Should().BeOfType<ProblemDetails>();
        var problemDetails = (ProblemDetails)notFoundResult.Value!;
        problemDetails.Title.Should().Be("Partner not found");
    }

    /// <summary>
    /// Проверить, что если партнер заблокирован, то возвращается 422 с корректно заполненным
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CreateLimit_WhenPartnerBlocked_ReturnsUnprocessableEntity()
    {
        // Arrange
        var partnerId = Guid.NewGuid();
        var request = new AutoFaker<PartnerPromoCodeLimitCreateRequest>().Generate();
        var partner = CreatePartner(partnerId, isActive: false);

        _partnersRepositoryMock
            .Setup(r => r.GetById(partnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        // Act
        var result = await _sut.CreateLimit(partnerId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnprocessableEntityObjectResult>();
        var unprocessableResult = (UnprocessableEntityObjectResult)result.Result;
        unprocessableResult.Value.Should().BeOfType<ProblemDetails>();
        var problemDetails = (ProblemDetails)unprocessableResult.Value!;
        problemDetails.Title.Should().Be("Partner blocked");
    }

    /// <summary>
    /// Проверить, что лимит успешно создается и возвращается 201 с корректно заполненным CreatedAtActionResult
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CreateLimit_WhenValidRequest_ReturnsCreatedAndAddsLimit()
    {
        // Arrange
        var partnerId = Guid.NewGuid();
        var request = new AutoFaker<PartnerPromoCodeLimitCreateRequest>()
            .RuleFor(r => r.EndAt, DateTimeOffset.UtcNow.AddDays(30))
            .Generate();
        var partner = CreatePartner(partnerId, isActive: true, withActiveLimits: false);

        _partnersRepositoryMock
            .Setup(r => r.GetById(partnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        PartnerPromoCodeLimit? addedLimit = null;
        _partnerLimitsRepositoryMock
            .Setup(r => r.Add(It.IsAny<PartnerPromoCodeLimit>(), It.IsAny<CancellationToken>()))
            .Callback<PartnerPromoCodeLimit, CancellationToken>((l, _) => addedLimit = l)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateLimit(partnerId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdAtResult = (CreatedAtActionResult)result.Result;
        createdAtResult.ActionName.Should().Be("GetLimit");
        createdAtResult.RouteValues.Should().ContainKey("partnerId").WhoseValue.Should().Be(partnerId);
        createdAtResult.RouteValues.Should().ContainKey("limitId").WhoseValue.Should().Be(addedLimit?.Id);

        addedLimit.Should().NotBeNull();
        addedLimit!.Limit.Should().Be(request.Limit);
        addedLimit.EndAt.Should().Be(request.EndAt);
        addedLimit.IssuedCount.Should().Be(0);

        _partnerLimitsRepositoryMock.Verify(r => r.Add(It.IsAny<PartnerPromoCodeLimit>(), It.IsAny<CancellationToken>()), Times.Once);
        _partnersRepositoryMock.Verify(r => r.Update(It.IsAny<Partner>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Проверить, что при создании нового лимита, старый отменяется
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CreateLimit_WhenValidRequestWithActiveLimits_CancelsOldLimitsAndAddsNew()
    {
        // Arrange
        var partnerId = Guid.NewGuid();
        var request = new AutoFaker<PartnerPromoCodeLimitCreateRequest>()
            .RuleFor(r => r.EndAt, DateTimeOffset.UtcNow.AddDays(30))
            .Generate();
        var oldLimit1 = CreateLimit(limitId: Guid.NewGuid(), canceledAt: null);
        var oldLimit2 = CreateLimit(limitId: Guid.NewGuid(), canceledAt: null);
        var partner = CreatePartner(partnerId, isActive: true, limits: [oldLimit1, oldLimit2]);

        _partnersRepositoryMock
            .Setup(r => r.GetById(partnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        Partner? updatedPartner = null;
        _partnersRepositoryMock
            .Setup(r => r.Update(It.IsAny<Partner>(), It.IsAny<CancellationToken>()))
            .Callback<Partner, CancellationToken>((p, _) => updatedPartner = p)
            .Returns(Task.CompletedTask);

        PartnerPromoCodeLimit? addedLimit = null;
        _partnerLimitsRepositoryMock
            .Setup(r => r.Add(It.IsAny<PartnerPromoCodeLimit>(), It.IsAny<CancellationToken>()))
            .Callback<PartnerPromoCodeLimit, CancellationToken>((l, _) => addedLimit = l)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateLimit(partnerId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();

        // Проверяем, что старые лимиты отменены
        oldLimit1.CanceledAt.Should().NotBeNull();
        oldLimit2.CanceledAt.Should().NotBeNull();

        // Проверяем, что партнер был обновлён
        _partnersRepositoryMock.Verify(r => r.Update(It.IsAny<Partner>(), It.IsAny<CancellationToken>()), Times.Once);
        updatedPartner.Should().BeSameAs(partner);

        // Проверяем, что новый лимит добавлен
        _partnerLimitsRepositoryMock.Verify(r => r.Add(It.IsAny<PartnerPromoCodeLimit>(), It.IsAny<CancellationToken>()), Times.Once);
        addedLimit.Should().NotBeNull();
        addedLimit!.Limit.Should().Be(request.Limit);
    }

    /// <summary>
    ///  Проверить, что при Update возникает EntityNotFoundException, то возвращается 404
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CreateLimit_WhenUpdateThrowsEntityNotFoundException_ReturnsNotFound()
    {
        // Arrange
        var partnerId = Guid.NewGuid();
        var request = new AutoFaker<PartnerPromoCodeLimitCreateRequest>()
            .RuleFor(r => r.EndAt, DateTimeOffset.UtcNow.AddDays(30))
            .Generate();
        var oldLimit = CreateLimit(limitId: Guid.NewGuid(), canceledAt: null);
        var partner = CreatePartner(partnerId, isActive: true, limits: [oldLimit]);

        _partnersRepositoryMock
            .Setup(r => r.GetById(partnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _partnersRepositoryMock
            .Setup(r => r.Update(It.IsAny<Partner>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException<Partner>(partnerId));

        // Act
        var result = await _sut.CreateLimit(partnerId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
        _partnerLimitsRepositoryMock.Verify(r => r.Add(It.IsAny<PartnerPromoCodeLimit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Partner CreatePartner(Guid partnerId, bool isActive, bool withActiveLimits = true, List<PartnerPromoCodeLimit>? limits = null)
    {
        var role = new AutoFaker<Role>()
            .RuleFor(r => r.Id, _ => Guid.NewGuid())
            .Generate();

        var employee = new AutoFaker<Employee>()
            .RuleFor(e => e.Id, _ => Guid.NewGuid())
            .RuleFor(e => e.Role, role)
            .Generate();

        var partnerLimits = limits ?? (withActiveLimits
            ? [CreateLimit(Guid.NewGuid(), canceledAt: null)]
            : []);

        return new AutoFaker<Partner>()
            .RuleFor(p => p.Id, _ => partnerId)
            .RuleFor(p => p.IsActive, _ => isActive)
            .RuleFor(p => p.Manager, employee)
            .RuleFor(p => p.PartnerLimits, partnerLimits)
            .Generate();
    }

    private static PartnerPromoCodeLimit CreateLimit(Guid limitId, DateTimeOffset? canceledAt)
    {
        return new AutoFaker<PartnerPromoCodeLimit>()
            .RuleFor(l => l.Id, _ => limitId)
            .RuleFor(l => l.CanceledAt, _ => canceledAt)
            .RuleFor(l => l.CreatedAt, _ => DateTimeOffset.UtcNow.AddDays(-10))
            .RuleFor(l => l.EndAt, _ => DateTimeOffset.UtcNow.AddDays(30))
            .RuleFor(l => l.Limit, _ => 100)
            .RuleFor(l => l.IssuedCount, _ => 0)
            .Generate();
    }
}
