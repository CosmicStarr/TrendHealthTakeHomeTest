using System.Text.Json;
using Data.Util;

public static class HTTpExtensions
{
    public static void AddPaginationHeader(this HttpResponse response, int currentPage, int itemsPerPage, int totalItems, int totalPages)
    {
        var paginationMetadata = new PagerHeader(currentPage,itemsPerPage,totalItems,totalPages);

        // Add the JSON string to the headers
        response.Headers.Append("X-Pagination", JsonSerializer.Serialize(paginationMetadata));
        
        // CRITICAL: You must expose this header, otherwise Angular's HttpClient will ignore it due to CORS security
        response.Headers.Append("Access-Control-Expose-Headers", "X-Pagination"); 
    }
}