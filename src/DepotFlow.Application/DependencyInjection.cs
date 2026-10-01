using DepotFlow.Application.Audit;
using DepotFlow.Application.Billing;
using DepotFlow.Application.Containers;
using DepotFlow.Application.Gate;
using DepotFlow.Application.Invoices;
using DepotFlow.Application.Reports;
using DepotFlow.Application.ShippingLines;
using DepotFlow.Application.Tariffs;
using DepotFlow.Application.Visits;
using DepotFlow.Application.Yard;
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

        services.AddScoped<GateInUseCase>();
        services.AddScoped<GateOutUseCase>();
        services.AddScoped<RelocateVisitUseCase>();
        services.AddScoped<GetContainerUseCase>();
        services.AddScoped<SearchContainersUseCase>();

        services.AddScoped<DailyMovementsReportUseCase>();
        services.AddScoped<YardOccupancyReportUseCase>();
        services.AddScoped<DwellTimeReportUseCase>();
        services.AddScoped<RevenueReportUseCase>();

        services.AddScoped<ListAuditLogsUseCase>();

        services.AddScoped<ChargeCalculator>();
        services.AddScoped<ChargePreviewUseCase>();

        services.AddScoped<ListInvoicesUseCase>();
        services.AddScoped<GetInvoiceUseCase>();
        services.AddScoped<PayInvoiceUseCase>();

        services.AddScoped<ListTariffsUseCase>();
        services.AddScoped<CreateTariffUseCase>();
        services.AddScoped<DeactivateTariffUseCase>();

        services.AddScoped<ListVisitsUseCase>();
        services.AddScoped<GetVisitUseCase>();

        services.AddScoped<ListYardSlotsUseCase>();
        services.AddScoped<GetYardOccupancyUseCase>();

        return services;
    }
}
