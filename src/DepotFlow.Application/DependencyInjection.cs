using DepotFlow.Application.ShippingLines;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DepotFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Finds every AbstractValidator<T> in this assembly.
        services.AddValidatorsFromAssemblyContaining<CreateShippingLineRequestValidator>();

        services.AddScoped<ListShippingLinesUseCase>();
        services.AddScoped<GetShippingLineUseCase>();
        services.AddScoped<CreateShippingLineUseCase>();
        services.AddScoped<UpdateShippingLineUseCase>();

        return services;
    }
}
