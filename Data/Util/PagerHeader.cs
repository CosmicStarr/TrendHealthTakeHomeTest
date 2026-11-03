using System;

namespace Data.Util;

public class PagerHeader(int currentPage, int itemsPerPage, int totalItems, int totalPages)
{
    public int CurrentPage { get; set; } = currentPage;
    public int ItemsPerPage { get; set; } = itemsPerPage;
    public int TotalItems { get; set; } = totalItems;
    public int TotalPages { get; set; } = totalPages;

}
