namespace Mwda.Control.Protocol;

public class AdapterProtocolException : Exception
{
    public AdapterProtocolException(string message)
        : base(message)
    {
    }

    public AdapterProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
