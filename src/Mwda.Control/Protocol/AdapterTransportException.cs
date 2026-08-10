namespace Mwda.Control.Protocol;

public sealed class AdapterTransportException : AdapterProtocolException
{
    public AdapterTransportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
