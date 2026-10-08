using AuthServer.Common.Exceptions;
using AuthServer.Common.Results;
using System.Net;
using System.Text.Json;

namespace AuthServer.Middleware
{

        // Global exception handling middleware.
        // This middleware sits early in the ASP.NET Core pipeline and catches
        // all unhandled exceptions thrown by controllers, services, or filters.
        public class ExceptionHandlingMiddleware
        {
            // Reference to the next middleware in the request pipeline
            private readonly RequestDelegate _next;

            // Logger used to log handled and unhandled exceptions
            private readonly ILogger<ExceptionHandlingMiddleware> _logger;

            // Constructor is called by the DI container.
            // ASP.NET Core injects the next middleware and logger automatically.
            public ExceptionHandlingMiddleware(
                RequestDelegate next,
                ILogger<ExceptionHandlingMiddleware> logger)
            {
                _next = next;
                _logger = logger;
            }

            // This method is executed for every HTTP request.
            // It wraps the remaining pipeline inside a try-catch block
            // to centrally handle all exceptions.
            public async Task Invoke(HttpContext context)
            {
                try
                {
                    // Pass control to the next middleware or endpoint
                    await _next(context);
                }
                catch (AppException ex)
                {
                    // AppException represents known business or validation errors.
                    // These are logged as warnings (not system failures).
                    _logger.LogWarning(
                        ex,
                        "Handled AppException. Path={Path}, Method={Method}, TraceId={TraceId}, StatusCode={StatusCode}, Message={Message}",
                        context.Request.Path,
                        context.Request.Method,
                        context.TraceIdentifier,
                        ex.StatusCode,
                        ex.Message
                    );

                    // Return a JSON response with the specific HTTP status code and message
                    await WriteErrorAsync(context, ex.StatusCode, ex.Message);
                }
                catch (Exception ex)
                {
                    // Any exception not explicitly handled is considered unexpected.
                    // These are logged as errors for investigation.
                    _logger.LogError(
                        ex,
                        "Unhandled exception. Path={Path}, Method={Method}, TraceId={TraceId}",
                        context.Request.Path,
                        context.Request.Method,
                        context.TraceIdentifier
                    );

                    // Never expose internal exception details to clients.
                    // Always return a generic error message for security.
                    await WriteErrorAsync(
                        context,
                        (int)HttpStatusCode.InternalServerError,
                        "Something went wrong. Please try again."
                    );
                }
            }

            // Writes a standardized JSON error response to the HTTP output stream
            private static async Task WriteErrorAsync(
                HttpContext context,
                int statusCode,
                string message)
            {
                // If the response has already started (headers/body sent),
                // we cannot safely write a JSON error response.
                if (context.Response.HasStarted)
                    return;

                // Clear any partially written response data
                context.Response.Clear();

                // Set the HTTP status code (400, 401, 403, 500, etc.)
                context.Response.StatusCode = statusCode;

                // Ensure the client receives JSON
                context.Response.ContentType = "application/json";

                // Wrap the error message using a standard API response structure
                // so, all error responses look the same across the application
                var payload = JsonSerializer.Serialize(
                    ApiResponse<string>.Fail(message)
                );

                // Write the serialized JSON response to the client
                await context.Response.WriteAsync(payload);
            }
        }

    
}
