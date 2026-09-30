using DepotFlow.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DepotFlow.Api.ErrorHandling;

public static class ResultExtensions
{
    /// <summary>Turns a failed result into the matching HTTP response, always with a "code" field.</summary>
    public static ObjectResult ToProblem(this ControllerBase controller, Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError
        };

        ProblemDetails problem = error.ValidationErrors is null
            ? controller.ProblemDetailsFactory.CreateProblemDetails(
                controller.HttpContext, status, title: error.Message)
            : controller.ProblemDetailsFactory.CreateValidationProblemDetails(
                controller.HttpContext, ToModelState(error.ValidationErrors), status, title: error.Message);

        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static ModelStateDictionary ToModelState(IReadOnlyDictionary<string, string[]> errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (field, messages) in errors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError(field, message);
            }
        }

        return modelState;
    }

    /// <summary>Success goes through <paramref name="onSuccess"/>; failure becomes a problem response.</summary>
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller, Result<T> result, Func<T, IActionResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : controller.ToProblem(result.Error);
}
