using System.Text.Json;
using BodegaAbierta.Application.Donors;
using BodegaAbierta.Application.Intakes;
using BodegaAbierta.Application.Inventory;
using BodegaAbierta.Application.Products;
using BodegaAbierta.Application.StorageLocations;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BodegaAbierta.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Error keys match the JSON the client sent: "lines[0].quantity", not "Lines[0].Quantity".
        ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
            member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<DonorService>();
        services.AddScoped<ProductService>();
        services.AddScoped<StorageLocationService>();
        services.AddScoped<DonationIntakeService>();
        services.AddScoped<InventoryLotService>();

        return services;
    }
}
