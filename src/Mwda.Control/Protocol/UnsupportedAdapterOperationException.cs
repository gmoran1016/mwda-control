using System.Net;

namespace Mwda.Control.Protocol;

public sealed class UnsupportedAdapterOperationException : AdapterProtocolException
{
    public UnsupportedAdapterOperationException(
        AdapterOperation operation,
        HttpStatusCode statusCode,
        string message)
        : base(message)
    {
        Operation = operation;
        StatusCode = statusCode;
    }

    public UnsupportedAdapterOperationException(
        AdapterOperation operation,
        HttpStatusCode statusCode,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Operation = operation;
        StatusCode = statusCode;
    }

    public AdapterOperation Operation { get; }

    public HttpStatusCode StatusCode { get; }
}
