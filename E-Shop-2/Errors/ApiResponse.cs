namespace E_Shop_2.Errors
{
    public class ApiResponse
    {
        public ApiResponse(int statusCode, string message = null)
        {
            StatusCode = statusCode;
            Message = message ?? GetDefaultMessageForStatusCode(statusCode); ;
        }

        public int StatusCode { get; }
        public string? Message { get; }

        private string? GetDefaultMessageForStatusCode(int statusCode) 
        {
            return statusCode switch
            {
                400 => "server could not understand the request",
                401 => "lacks valid authentication credentials for the requested resource",
                404 => "Resource was not found",
                500 => "Server encountered an unexpected condition that prevented it from fulfilling the request",
                _ => null
            };
        }
    }
}
