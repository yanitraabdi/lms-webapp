using Academy.Application.Admin;
using Academy.Application.Auth;
using Academy.Application.Billing;
using Academy.Application.Learning;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Academy.Api;

/// <summary>Maps domain auth/billing failures + FluentValidation errors to RFC-7807 problem details.</summary>
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
            case BillingException be:
                problem = new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = be.Message };
                break;
            case LearningException le:
                problem = new ProblemDetails { Status = le.StatusCode, Title = le.Message };
                break;
            case AdminException adm:
                problem = new ProblemDetails { Status = adm.StatusCode, Title = adm.Message };
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
