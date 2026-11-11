using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Beneficiaries;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Application.Distributions;
using BodegaAbierta.Application.Donors;
using BodegaAbierta.Application.Intakes;
using BodegaAbierta.Application.Inventory;
using BodegaAbierta.Application.Policies;
using BodegaAbierta.Application.Products;
using BodegaAbierta.Application.Rules;
using BodegaAbierta.Domain.Beneficiaries;
using BodegaAbierta.Domain.Donors;
using BodegaAbierta.Domain.Inventory;
using BodegaAbierta.Domain.Policies;
using BodegaAbierta.Domain.Products;
using BodegaAbierta.IntegrationTests.Infrastructure;

namespace BodegaAbierta.IntegrationTests;

/// <summary>Phase 4 end to end: Active policy + FEFO allocation + pickup slots + /policies/evaluate.</summary>
public sealed class RulesEngineEndpointsTests(SqlServerFixture sql) : ApiTestBase(sql)
{
    private static readonly TimeSpan Mexico = TimeSpan.FromHours(-6);

    private const string Fefo = "{}";
    private const string ExpiryAlert3Days = """{"daysBeforeExpiry":3}""";
    private const string TueThuMornings = """{"days":["Tuesday","Thursday"],"open":"09:00","close":"14:00","maxSlotCapacity":20}""";
    private const string TenKgPerWeek = """{"maxKg":10,"periodDays":7}""";
    private const string WeekdayMornings = """{"days":["Monday","Tuesday","Wednesday","Thursday","Friday"],"open":"08:00","close":"12:00"}""";

    private sealed record World(TestTenant Tenant, HttpClient Staff, HttpClient Coordinator, Guid DonorId, Guid BeansId, Guid MilkId, Guid BeneficiaryId);

    /// <summary>Next Tuesday at 10:00 Mexico City (always in the future, never today).</summary>
    private static DateTimeOffset NextTuesdayTen()
    {
        var nowMx = DateTimeOffset.UtcNow.ToOffset(Mexico);
        var days = ((int)DayOfWeek.Tuesday - (int)nowMx.DayOfWeek + 7) % 7;
        var date = nowMx.Date.AddDays(days == 0 ? 7 : days);
        return new DateTimeOffset(date.Year, date.Month, date.Day, 10, 0, 0, Mexico);
    }

    private static DateOnly DateOf(DateTimeOffset value) => DateOnly.FromDateTime(value.DateTime);

    private static async Task<T> CreatedAsync<T>(Task<HttpResponseMessage> call)
    {
        var response = await call;
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return await ReadAsync<T>(response);
    }

    private async Task<World> CreateWorldAsync()
    {
        var tenant = await CreateTenantAsync();
        var staff = CreateClientAs(tenant, "ana");
        var coordinator = CreateClientAs(tenant, "coordinadora", Roles.Coordinator);

        var donor = await CreatedAsync<DonorResponse>(PostAsync(staff, "/api/v1/donors",
            new CreateDonorRequest("Supermercado La Esperanza", DonorType.Business, null, null)));
        var beans = await CreatedAsync<ProductResponse>(PostAsync(staff, "/api/v1/products",
            new CreateProductRequest("Frijol negro", ProductCategory.Grains, UnitOfMeasure.Kilogram, IsPerishable: false)));
        var milk = await CreatedAsync<ProductResponse>(PostAsync(staff, "/api/v1/products",
            new CreateProductRequest("Leche entera 1 L", ProductCategory.Dairy, UnitOfMeasure.Unit, IsPerishable: true)));
        var beneficiary = await CreatedAsync<BeneficiaryResponse>(PostAsync(staff, "/api/v1/beneficiaries",
            new CreateBeneficiaryRequest("Familia López", BeneficiaryType.Household, null)));

        return new World(tenant, staff, coordinator, donor.Id, beans.Id, milk.Id, beneficiary.Id);
    }

    private static Task<DonationIntakeResponse> ReceiveAsync(World w, params DonationIntakeLineRequest[] lines)
        => CreatedAsync<DonationIntakeResponse>(PostAsync(w.Staff, "/api/v1/donation-intakes",
            new CreateDonationIntakeRequest(w.DonorId, "Ana (almacén)", null, null, lines)));

    private static DonationIntakeLineRequest Line(World w, Guid productId, decimal quantity, DateOnly? expiresOn = null)
        => new(productId, w.Tenant.StorageLocationId, quantity, expiresOn);

    private static Task<PickupSlotResponse> CreateSlotAsync(World w, DateTimeOffset startsAt, int capacity = 5)
        => CreatedAsync<PickupSlotResponse>(PostAsync(w.Staff, "/api/v1/pickup-slots",
            new CreatePickupSlotRequest(startsAt, startsAt.AddHours(2), capacity)));

    private static async Task<PolicySetResponse> ActivatePolicyAsync(World w, params (RuleType Type, string Json)[] rules)
    {
        var draft = await CreatedAsync<PolicySetResponse>(PostAsync(w.Staff, "/api/v1/policies", new CreatePolicySetRequest(
            "Reglas de operación",
            rules.Select(r => new PolicyRuleRequest(r.Type, JsonDocument.Parse(r.Json).RootElement)).ToList(),
            null)));

        var submitted = await SendWithIfMatchAsync(w.Staff, HttpMethod.Post, $"/api/v1/policies/{draft.Id}/submit", $"\"{draft.RowVersion}\"");
        submitted.StatusCode.Should().Be(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync(Ct));
        var pending = await ReadAsync<PolicySetResponse>(submitted);

        var approved = await SendWithIfMatchAsync(w.Coordinator, HttpMethod.Post, $"/api/v1/policies/{pending.Id}/approve", $"\"{pending.RowVersion}\"");
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync(Ct));
        return await ReadAsync<PolicySetResponse>(approved);
    }

    private static Task<HttpResponseMessage> DistributeAsync(HttpClient client, Guid beneficiaryId, Guid slotId, params DistributionLineRequest[] lines)
        => PostAsync(client, "/api/v1/distributions", new CreateDistributionRequest(beneficiaryId, slotId, lines));

    private static async Task<PolicyEvaluationResponse> EvaluateAsync(HttpClient client, EvaluatePolicyRequest request)
    {
        var response = await PostAsync(client, "/api/v1/policies/evaluate", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await ReadAsync<PolicyEvaluationResponse>(response);
    }

    private static async Task<IReadOnlyList<string>> ViolationCodesAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync(Ct));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("code").GetString().Should().Be("policy.violation");
        return body.RootElement.GetProperty("violations").EnumerateArray()
            .Select(v => v.GetProperty("code").GetString()!)
            .ToList();
    }

    [Fact]
    public async Task Evaluate_without_policy_previews_the_fefo_allocation_and_saves_nothing()
    {
        var w = await CreateWorldAsync();
        var pickup = NextTuesdayTen();
        var intake = await ReceiveAsync(w,
            Line(w, w.MilkId, 10, DateOf(pickup).AddDays(20)),
            Line(w, w.MilkId, 10, DateOf(pickup).AddDays(2)));
        var soonLot = intake.Lots.Single(l => l.ExpiresOn == DateOf(pickup).AddDays(2));
        var laterLot = intake.Lots.Single(l => l.ExpiresOn == DateOf(pickup).AddDays(20));
        var slot = await CreateSlotAsync(w, pickup);

        var result = await EvaluateAsync(w.Staff, new EvaluatePolicyRequest(
            EvaluationKind.Distribution, BeneficiaryId: w.BeneficiaryId, PickupSlotId: slot.Id,
            Lines: [new DistributionLineRequest(w.MilkId, 12, null)]));

        result.Allowed.Should().BeTrue();
        result.PolicySetId.Should().BeNull();
        result.Violations.Should().BeEmpty();
        result.Allocation.Select(a => (a.InventoryLotId, a.Quantity)).Should().Equal((soonLot.Id, 10m), (laterLot.Id, 2m));

        var unchanged = await w.Staff.GetFromJsonAsync<InventoryLotResponse>($"/api/v1/inventory-lots/{soonLot.Id}", Json, Ct);
        unchanged!.QuantityOnHand.Should().Be(10);
    }

    [Fact]
    public async Task Distribution_allocates_fefo_books_the_slot_and_enforces_the_beneficiary_limit()
    {
        var w = await CreateWorldAsync();
        var pickup = NextTuesdayTen();
        var slot = await CreateSlotAsync(w, pickup);
        var intake = await ReceiveAsync(w,
            Line(w, w.MilkId, 10, DateOf(pickup).AddDays(2)),
            Line(w, w.MilkId, 10, DateOf(pickup).AddDays(20)),
            Line(w, w.BeansId, 20));
        var soonMilk = intake.Lots.Single(l => l.ExpiresOn == DateOf(pickup).AddDays(2));

        var policy = await ActivatePolicyAsync(w,
            (RuleType.FefoAllocation, Fefo),
            (RuleType.ExpiryAlert, ExpiryAlert3Days),
            (RuleType.PickupWindow, TueThuMornings),
            (RuleType.BeneficiaryLimit, TenKgPerWeek));

        var distribution = await CreatedAsync<DistributionResponse>(DistributeAsync(w.Staff, w.BeneficiaryId, slot.Id,
            new DistributionLineRequest(w.MilkId, 12, null),
            new DistributionLineRequest(w.BeansId, 6, null)));

        distribution.PolicySetId.Should().Be(policy.Id);
        distribution.CreatedBy.Should().Be("ana");
        distribution.Allocations.Where(a => a.ProductId == w.MilkId).Select(a => a.Quantity).Should().BeEquivalentTo([10m, 2m]);
        distribution.Allocations.Single(a => a.ProductId == w.BeansId).Quantity.Should().Be(6);
        distribution.Warnings.Should().Contain(v => v.Code == "expiry.soon" && v.Severity == ViolationSeverity.Warning);

        var soonAfter = await w.Staff.GetFromJsonAsync<InventoryLotResponse>($"/api/v1/inventory-lots/{soonMilk.Id}", Json, Ct);
        soonAfter!.QuantityOnHand.Should().Be(0);
        soonAfter.Status.Should().Be(LotStatus.Distributed);
        (await w.Staff.GetFromJsonAsync<PickupSlotResponse>($"/api/v1/pickup-slots/{slot.Id}", Json, Ct))!.ReservedCount.Should().Be(1);

        // 6 kg already scheduled this week + 6 kg more = 12 kg > 10 kg.
        var second = await DistributeAsync(w.Staff, w.BeneficiaryId, slot.Id, new DistributionLineRequest(w.BeansId, 6, null));

        (await ViolationCodesAsync(second)).Should().Contain("beneficiary.limit_exceeded");
    }

    [Fact]
    public async Task Picking_a_later_expiring_lot_by_hand_breaks_fefo()
    {
        var w = await CreateWorldAsync();
        var pickup = NextTuesdayTen();
        var slot = await CreateSlotAsync(w, pickup);
        var intake = await ReceiveAsync(w,
            Line(w, w.MilkId, 10, DateOf(pickup).AddDays(2)),
            Line(w, w.MilkId, 10, DateOf(pickup).AddDays(20)));
        var laterLot = intake.Lots.Single(l => l.ExpiresOn == DateOf(pickup).AddDays(20));
        await ActivatePolicyAsync(w, (RuleType.FefoAllocation, Fefo));

        var result = await EvaluateAsync(w.Staff, new EvaluatePolicyRequest(
            EvaluationKind.Distribution, BeneficiaryId: w.BeneficiaryId, PickupSlotId: slot.Id,
            Lines: [new DistributionLineRequest(w.MilkId, 3, laterLot.Id)]));

        result.Allowed.Should().BeFalse();
        result.Violations.Should().ContainSingle().Which.Should().Match<RuleViolation>(v =>
            v.Code == "fefo.not_first_expiring" && v.RuleType == RuleType.FefoAllocation && v.LineIndex == 0);
    }

    [Fact]
    public async Task Pickup_slots_must_fit_the_pickup_window()
    {
        var w = await CreateWorldAsync();
        var tuesday = NextTuesdayTen();
        await ActivatePolicyAsync(w, (RuleType.PickupWindow, TueThuMornings));

        var wednesday = await PostAsync(w.Staff, "/api/v1/pickup-slots", new CreatePickupSlotRequest(tuesday.AddDays(1), tuesday.AddDays(1).AddHours(2), 5));
        (await ViolationCodesAsync(wednesday)).Should().Equal("pickup.outside_window");

        var tooBig = await PostAsync(w.Staff, "/api/v1/pickup-slots", new CreatePickupSlotRequest(tuesday, tuesday.AddHours(2), 30));
        (await ViolationCodesAsync(tooBig)).Should().Equal("pickup.capacity_exceeds_max");

        await CreateSlotAsync(w, tuesday, capacity: 20);

        var preview = await EvaluateAsync(w.Staff, new EvaluatePolicyRequest(
            EvaluationKind.PickupSlot, StartsAt: tuesday.AddHours(5), EndsAt: tuesday.AddHours(6), Capacity: 5));
        preview.Allowed.Should().BeFalse("15:00-16:00 is after the 14:00 close");
    }

    [Fact]
    public async Task Built_in_checks_apply_even_without_a_policy()
    {
        var w = await CreateWorldAsync();
        var slot = await CreateSlotAsync(w, NextTuesdayTen(), capacity: 1);
        await ReceiveAsync(w, Line(w, w.BeansId, 5));
        var otherFamily = await CreatedAsync<BeneficiaryResponse>(PostAsync(w.Staff, "/api/v1/beneficiaries",
            new CreateBeneficiaryRequest("Comedor San Martín", BeneficiaryType.Organization, null)));

        var tooMuch = await DistributeAsync(w.Staff, w.BeneficiaryId, slot.Id, new DistributionLineRequest(w.BeansId, 8, null));
        (await ViolationCodesAsync(tooMuch)).Should().Equal("inventory.insufficient");

        await CreatedAsync<DistributionResponse>(DistributeAsync(w.Staff, w.BeneficiaryId, slot.Id, new DistributionLineRequest(w.BeansId, 2, null)));

        var slotFull = await DistributeAsync(w.Staff, otherFamily.Id, slot.Id, new DistributionLineRequest(w.BeansId, 1, null));
        (await ViolationCodesAsync(slotFull)).Should().Equal("slot.full");
    }

    [Fact]
    public async Task Donations_outside_drop_off_hours_are_rejected()
    {
        var w = await CreateWorldAsync();
        await ActivatePolicyAsync(w, (RuleType.DonorDropOffHours, WeekdayMornings));
        var pastTuesday = NextTuesdayTen().AddDays(-14);

        var evening = await PostAsync(w.Staff, "/api/v1/donation-intakes", new CreateDonationIntakeRequest(
            w.DonorId, "Ana", pastTuesday.AddHours(8), null, [Line(w, w.BeansId, 5)]));
        (await ViolationCodesAsync(evening)).Should().Equal("donor.outside_dropoff_hours");

        var morning = await EvaluateAsync(w.Staff, new EvaluatePolicyRequest(EvaluationKind.DonorDropOff, DonorId: w.DonorId, At: pastTuesday));
        morning.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Rule_parameters_are_validated_per_rule_type()
    {
        var w = await CreateWorldAsync();

        var response = await PostAsync(w.Staff, "/api/v1/policies", new CreatePolicySetRequest(null,
            [new PolicyRuleRequest(RuleType.ExpiryAlert, JsonDocument.Parse("""{"daysBeforeExpiri":3}""").RootElement)], null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("errors").TryGetProperty("rules[0].parameters", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Concurrent_distributions_never_oversell_the_last_stock()
    {
        var w = await CreateWorldAsync();
        var slot = await CreateSlotAsync(w, NextTuesdayTen(), capacity: 10);
        var intake = await ReceiveAsync(w, Line(w, w.BeansId, 5));
        var other = await CreatedAsync<BeneficiaryResponse>(PostAsync(w.Staff, "/api/v1/beneficiaries",
            new CreateBeneficiaryRequest("Comedor San Martín", BeneficiaryType.Organization, null)));

        using var secondClient = CreateClientAs(w.Tenant, "beto");
        var responses = await Task.WhenAll(
            DistributeAsync(w.Staff, w.BeneficiaryId, slot.Id, new DistributionLineRequest(w.BeansId, 5, null)),
            DistributeAsync(secondClient, other.Id, slot.Id, new DistributionLineRequest(w.BeansId, 5, null)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Single(r => r.StatusCode != HttpStatusCode.Created).StatusCode
            .Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);

        var lot = await w.Staff.GetFromJsonAsync<InventoryLotResponse>($"/api/v1/inventory-lots/{intake.Lots.Single().Id}", Json, Ct);
        lot!.QuantityOnHand.Should().Be(0);
    }

    [Fact]
    public async Task Expiry_alerts_use_the_active_policy_days()
    {
        var w = await CreateWorldAsync();
        var todayMx = DateOf(DateTimeOffset.UtcNow.ToOffset(Mexico));
        var intake = await ReceiveAsync(w,
            Line(w, w.MilkId, 10, todayMx.AddDays(2)),
            Line(w, w.MilkId, 10, todayMx.AddDays(20)));

        var withoutRule = await w.Staff.GetFromJsonAsync<ExpiryAlertsResponse>("/api/v1/inventory-lots/expiry-alerts", Json, Ct);
        withoutRule!.DaysBeforeExpiry.Should().BeNull();
        withoutRule.Lots.Should().BeEmpty();

        await ActivatePolicyAsync(w, (RuleType.ExpiryAlert, ExpiryAlert3Days));

        var alerts = await w.Staff.GetFromJsonAsync<ExpiryAlertsResponse>("/api/v1/inventory-lots/expiry-alerts", Json, Ct);
        alerts!.DaysBeforeExpiry.Should().Be(3);
        alerts.Lots.Should().ContainSingle().Which.Id.Should().Be(intake.Lots.Single(l => l.ExpiresOn == todayMx.AddDays(2)).Id);
    }
}
