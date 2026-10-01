namespace DepotFlow.Application.Common;

/// <summary>
/// Names of the unique indexes the application reacts to. Infrastructure uses the same constants when it
/// creates them, so a violation can be matched to a business error.
/// </summary>
public static class IndexNames
{
    public const string VisitContainerActive = "UX_Visits_Container_Active";
    public const string VisitSlotActive = "UX_Visits_Slot_Active";
    public const string InvoiceVisit = "UX_Invoices_Visit";
    public const string TariffLineSizeActive = "UX_Tariffs_Line_Size_Active";
}
