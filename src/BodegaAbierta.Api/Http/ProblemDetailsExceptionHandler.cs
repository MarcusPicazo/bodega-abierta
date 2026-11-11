using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Application.Rules;
using BodegaAbierta.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Api.Http;

/// <summary>
/// The single place that turns known exceptions into RFC 9457 ProblemDetails responses.
/// Unknown exceptions return false and fall through to the default 500 handler.
/// </summary>
internal sealed class ProblemDetailsExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private const int SqlUniqueIndexViolation = 2601;
    private const int SqlUniqueConstraintViolation = 2627;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => new HttpValidationProblemDetails(
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },

            TenantNotResolvedException e => Create(StatusCodes.Status400BadRequest, "Food bank not resolved", e.Message),

            UnauthenticatedException e => Create(StatusCodes.Status401Unauthorized, "Unauthorized", e.Message),

            ForbiddenException e => Create(StatusCodes.Status403Forbidden, "Forbidden", e.Message),

            NotFoundException e => Create(StatusCodes.Status404NotFound, "Resource not found", e.Message),

            PolicyViolationException e => WithViolations(
                WithCode(Create(StatusCodes.Status422UnprocessableEntity, "Policy violation", e.Message), "policy.violation"),
                e.Violations),

            ConcurrencyConflictException e => Create(StatusCodes.Status409Conflict, "Concurrent change", e.Message),

            DomainRuleException e => WithCode(
                Create(StatusCodes.Status422UnprocessableEntity, "Business rule violated", e.Message), e.Code),

            // Must come before DbUpdateException: it is a subclass.
            DbUpdateConcurrencyException or PreconditionFailedException => Create(
                StatusCodes.Status412PreconditionFailed,
                "Precondition failed",
                exception is PreconditionFailedException
                    ? exception.Message
                    : "The resource was modified by someone else. GET it again for the current version and retry."),

            PreconditionRequiredException e => Create(StatusCodes.Status428PreconditionRequired, "Precondition required", e.Message),

            DbUpdateException { InnerException: SqlException { Number: SqlUniqueIndexViolation or SqlUniqueConstraintViolation } }
                => Create(StatusCodes.Status409Conflict, "Conflict", "A resource with the same unique value already exists."),

            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Create(int status, string title, string detail)
        => new() { Status = status, Title = title, Detail = detail };

    private static ProblemDetails WithCode(ProblemDetails problem, string code)
    {
        problem.Extensions["code"] = code;
        return problem;
    }

    private static ProblemDetails WithViolations(ProblemDetails problem, IReadOnlyList<RuleViolation> violations)
    {
        problem.Extensions["violations"] = violations;
        return problem;
    }
}
