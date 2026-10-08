namespace AuthServer.Common.Results
{
    public class ApiResponse <T>
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = "OK";
        public T? Data { get; set; }

        public static ApiResponse<T> Success(T data, string message = "OK")
        {
            return new ApiResponse<T>() { IsSuccess = true, Message = message, Data = data };
        }

        public static ApiResponse<T> Fail(string message)
        {
            return new ApiResponse<T>() { IsSuccess = false, Message = message };
        }

    }
}
