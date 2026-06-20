using Microsoft.AspNetCore.Mvc.Rendering;

namespace GS_CookieOrder_Tracker.Models;

public class OrdersIndexViewModel
{
    public List<GS_CookieOrder_Tracker.Data.Order> Orders { get; set; } = new();

    // KPI (based on filtered results)
    public int TotalOrders { get; set; }
    public int TotalBoxesSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCollected { get; set; }

    // Pagination
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalPages { get; set; }
    public int TotalFilteredCount { get; set; }

    // Sorting
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }

    // Filters
    public string? SearchTerm { get; set; }
    public string? OrderTypeFilter { get; set; }
    public string? StatusFilter { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    // Filter dropdown options
    public List<SelectListItem> OrderTypes { get; set; } = new();
    public List<SelectListItem> Statuses { get; set; } = new();
}
