namespace GestorCampo.Application.Common;

public class ServiceResult
{
    public bool Succeeded { get; private set; }
    public string? Error { get; private set; }

    private ServiceResult(bool succeeded, string? error = null)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public static ServiceResult Ok() => new(true);
    public static ServiceResult Fail(string error) => new(false, error);
}

public class ServiceResult<T>
{
    public bool Succeeded { get; private set; }
    public T? Data { get; private set; }
    public string? Error { get; private set; }

    private ServiceResult(bool succeeded, T? data = default, string? error = null)
    {
        Succeeded = succeeded;
        Data = data;
        Error = error;
    }

    public static ServiceResult<T> Ok(T data) => new(true, data);
    public static ServiceResult<T> Fail(string error) => new(false, error: error);
}
