namespace PayFlow.Application.Exceptions;

public class PayFlowException : Exception
{
    public PayFlowException(string message) : base(message) { }
    public PayFlowException(string message, Exception inner) : base(message, inner) { }
}

public class NotFoundException : PayFlowException
{
    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} with key '{key}' was not found.") { }
}

public class ConflictException : PayFlowException
{
    public ConflictException(string message) : base(message) { }
}

public class ValidationException : PayFlowException
{
    public ValidationException(string message) : base(message) { }
}

public class ConcurrencyException : PayFlowException
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception inner) : base(message, inner) { }
}

public class InsufficientFundsException : PayFlowException
{
    public InsufficientFundsException(string ownerId, decimal required, decimal available)
        : base($"Wallet for owner '{ownerId}' has insufficient funds. Required: {required}, Available: {available}.") { }
}
