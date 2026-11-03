
namespace CosmicStoreAPI.Error
{
    public class ApiErrorResponse
    {
        public ApiErrorResponse(int statusCode, string? message = null)
        {
            StatusCode = statusCode;
            Message = !string.IsNullOrEmpty(message) ? message : ErrorMessageResponse(statusCode);
        }

        public int StatusCode { get; set; }
        public string Message { get; set; }

        private string ErrorMessageResponse(int statusCode)
        {
           return statusCode switch 
           {
               400 => "You made a bad request!",
               401 => "You are not authorized!",
               404 => "What you are looking for does not exist!",
               409 => "The request conflicts with the current state of the resource.",
               500 => "The Errors that are made are from our end!",
               _=> string.Empty
           };
        }
    }
}