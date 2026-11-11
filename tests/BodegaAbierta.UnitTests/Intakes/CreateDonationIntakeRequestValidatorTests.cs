using BodegaAbierta.Application;
using BodegaAbierta.Application.Intakes;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace BodegaAbierta.UnitTests.Intakes;

public sealed class CreateDonationIntakeRequestValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static IValidator<CreateDonationIntakeRequest> CreateValidator()
    {
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Now);

        // Resolve through DI so the test also covers registration and the camelCase name resolver.
        var services = new ServiceCollection().AddSingleton(clock).AddApplication();
        return services.BuildServiceProvider().GetRequiredService<IValidator<CreateDonationIntakeRequest>>();
    }

    private static DonationIntakeLineRequest Line(decimal quantity = 10m)
        => new(Guid.NewGuid(), Guid.NewGuid(), quantity, new DateOnly(2026, 12, 1));

    private static CreateDonationIntakeRequest Request(
        IReadOnlyList<DonationIntakeLineRequest>? lines = null, DateTimeOffset? receivedAt = null, string receivedBy = "Ana")
        => new(Guid.NewGuid(), receivedBy, receivedAt, null, lines ?? [Line()]);

    [Fact]
    public async Task Valid_request_passes()
    {
        var result = await CreateValidator().ValidateAsync(Request(), TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Intake_without_lines_fails()
    {
        var result = await CreateValidator().ValidateAsync(Request(lines: []), TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle(e => e.PropertyName == "lines");
    }

    [Fact]
    public async Task Line_errors_use_camelCase_json_paths()
    {
        var result = await CreateValidator().ValidateAsync(Request(lines: [Line(), Line(quantity: 0)]), TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle(e => e.PropertyName == "lines[1].quantity");
    }

    [Fact]
    public async Task More_than_three_decimals_fails()
    {
        var result = await CreateValidator().ValidateAsync(Request(lines: [Line(quantity: 1.2345m)]), TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle(e => e.PropertyName == "lines[0].quantity");
    }

    [Fact]
    public async Task ReceivedAt_in_the_future_fails_beyond_clock_skew()
    {
        var validator = CreateValidator();

        var slightlyAhead = await validator.ValidateAsync(Request(receivedAt: Now.AddMinutes(3)), TestContext.Current.CancellationToken);
        var future = await validator.ValidateAsync(Request(receivedAt: Now.AddHours(1)), TestContext.Current.CancellationToken);

        slightlyAhead.IsValid.Should().BeTrue();
        future.Errors.Should().ContainSingle(e => e.PropertyName == "receivedAt");
    }

    [Fact]
    public async Task ReceivedBy_is_required()
    {
        var result = await CreateValidator().ValidateAsync(Request(receivedBy: " "), TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle(e => e.PropertyName == "receivedBy");
    }
}
