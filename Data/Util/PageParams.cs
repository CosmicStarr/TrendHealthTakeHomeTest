using System;

namespace Data.Util;

public class PageParams
{
    const int MaxPageSize = 50;
    public int PageNumber { get; set; } =1;
    private int _PageSize = 10;
    public int PageSize
    {
        get
        {
            return _PageSize;
        }
        set
        {
            _PageSize=(value > MaxPageSize)?MaxPageSize:value;
        }
    }

    public string? Sort { get; set; }
    public string? Search { get; set; }
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool ClearCache { get; set; } = false;
}
