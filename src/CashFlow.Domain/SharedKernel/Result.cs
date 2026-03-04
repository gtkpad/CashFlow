namespace CashFlow.Domain.SharedKernel;

public class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        Error = null;
    }

    private Result(string error)
    {
        IsSuccess = false;
        _value = default;
        Error = error;
    }

    public bool IsSuccess { get; }
    public string? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of a failed result.");

    internal static Result<T> Success(T value) => new(value);
    internal static Result<T> Failure(string error) => new(error);
}

public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
}
