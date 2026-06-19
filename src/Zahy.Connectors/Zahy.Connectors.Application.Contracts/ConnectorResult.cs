namespace Zahy.Connectors;

public enum ConnectorErrorCode
{
    None = 0,
    NotSupported = 1,
    NotFound = 2,
    InvalidRequest = 3,
    OperationFailed = 4
}

public sealed class ConnectorResult<T>
{
    public bool Success { get; init; }

    public T? Value { get; init; }

    public ConnectorErrorCode ErrorCode { get; init; }

    public string? Message { get; init; }

    public static ConnectorResult<T> Ok(T value) =>
        new() { Success = true, Value = value, ErrorCode = ConnectorErrorCode.None };

    public static ConnectorResult<T> NotSupported(string operation) =>
        new()
        {
            Success = false,
            ErrorCode = ConnectorErrorCode.NotSupported,
            Message = operation
        };

    public static ConnectorResult<T> Fail(ConnectorErrorCode code, string message) =>
        new() { Success = false, ErrorCode = code, Message = message };
}

public sealed class ConnectorResult
{
    public bool Success { get; init; }

    public ConnectorErrorCode ErrorCode { get; init; }

    public string? Message { get; init; }

    public static ConnectorResult Ok() =>
        new() { Success = true, ErrorCode = ConnectorErrorCode.None };

    public static ConnectorResult NotSupported(string operation) =>
        new()
        {
            Success = false,
            ErrorCode = ConnectorErrorCode.NotSupported,
            Message = operation
        };
}
