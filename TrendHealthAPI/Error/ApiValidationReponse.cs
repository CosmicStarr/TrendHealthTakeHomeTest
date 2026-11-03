namespace CosmicStoreAPI.Error
{
    /// <summary>
    /// 400 response body. <see cref="Errors"/> maps each invalid field (camelCase, as it appears in the
    /// JSON or query string) to its error messages, e.g. <c>{ "title": ["The title field is required."] }</c>.
    /// </summary>
    public class ApiValidationResponse : ApiErrorResponse
    {
        public ApiValidationResponse() : base(400, "One or more validation errors occurred.")
        {
            Errors = new Dictionary<string, string[]>();
        }

        public ApiValidationResponse(IDictionary<string, string[]> errors) : this()
        {
            Errors = errors;
        }

        public IDictionary<string, string[]> Errors { get; set; }
    }
}
