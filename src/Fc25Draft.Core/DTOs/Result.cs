namespace Fc25Draft.Core.DTOs;

public sealed record Result(bool Success, string Message)
{
    public static Result Ok(string message) => new(true, message);

    public static Result Fail(string message) => new(false, message);
}
