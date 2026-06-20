namespace NexConvo.BuildingBlocks.Results;

/// <summary>Outcome category, mapped to HTTP status codes at the API edge.</summary>
public enum ResultStatus
{
    Success,
    NotFound,
    Conflict,
    Validation,
    Unauthorized,
    Forbidden,
    Error,
}

/// <summary>
/// The result of an application operation. Handlers return this instead of throwing for
/// expected outcomes (not-found, conflict, validation), so the API can map cleanly to
/// 200/404/409/422 without exception-driven control flow.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, ResultStatus status, string? error)
    {
        IsSuccess = isSuccess;
        Status = status;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public ResultStatus Status { get; }
    public string? Error { get; }

    public static Result Success() => new(true, ResultStatus.Success, null);
    public static Result NotFound(string? error = null) => new(false, ResultStatus.NotFound, error);
    public static Result Conflict(string? error = null) => new(false, ResultStatus.Conflict, error);
    public static Result Invalid(string error) => new(false, ResultStatus.Validation, error);
    public static Result Unauthorized(string? error = null) => new(false, ResultStatus.Unauthorized, error);
    public static Result Forbidden(string? error = null) => new(false, ResultStatus.Forbidden, error);
    public static Result Failure(string error) => new(false, ResultStatus.Error, error);

    /// <summary>Success carrying a value.</summary>
    public static Result<T> Success<T>(T value) => Result<T>.FromValue(value);
}

/// <summary>A <see cref="Result"/> that carries a value on success.</summary>
public sealed class Result<T> : Result
{
    private Result(bool isSuccess, ResultStatus status, T? value, string? error)
        : base(isSuccess, status, error) => Value = value;

    public T? Value { get; }

    internal static Result<T> FromValue(T value) => new(true, ResultStatus.Success, value, null);

    public static Result<T> Success(T value) => FromValue(value);
    public static new Result<T> NotFound(string? error = null) => new(false, ResultStatus.NotFound, default, error);
    public static new Result<T> Conflict(string? error = null) => new(false, ResultStatus.Conflict, default, error);
    public static new Result<T> Invalid(string error) => new(false, ResultStatus.Validation, default, error);
    public static new Result<T> Unauthorized(string? error = null) => new(false, ResultStatus.Unauthorized, default, error);
    public static new Result<T> Forbidden(string? error = null) => new(false, ResultStatus.Forbidden, default, error);
    public static new Result<T> Failure(string error) => new(false, ResultStatus.Error, default, error);
}
