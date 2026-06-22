using Academy.Application.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Academy.Api;

/// <summary>Maps domain auth failures + FluentValidation errors to RFC-7807 problem details.</summary>
public class AuthExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        switch (exception)
        {
            case AuthException ae:
                problem = new ProblemDetails { Status = ae.StatusCode, Title = ae.Message };
                problem.Extensions["code"] = ae.Code;
                break;
            case ValidationException ve:
                problem = new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "Validasi gagal." };
                problem.Extensions["errors"] = ve.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                break;
            default:
                return false; // not handled → default 500 pipeline
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
