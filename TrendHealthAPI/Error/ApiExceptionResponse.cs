namespace CosmicStoreAPI.Error
{
    public class ApiExceptionResponse : ApiErrorResponse
    {
        public ApiExceptionResponse(int statusCode, string? message = null, string? details = null):base(statusCode,message)
        {
            Details = details ?? string.Empty;
        }
        public string Details { get; set; }
    }
}