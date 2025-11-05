using Grpc.Core;
using Grpc.Core.Interceptors;
using LogManager.Enrichers;

namespace LogManager.Interceptors;

/// <summary>
/// gRPC interceptor to capture and propagate correlation ID
/// </summary>
public class CorrelationIdInterceptor : Interceptor
{
    private const string CorrelationIdMetadataKey = "x-correlation-id";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var correlationId = GetOrCreateCorrelationId(context.RequestHeaders);
        
        // Set correlation ID in AsyncLocal storage
        DefaultCorrelationIdAccessor.SetCorrelationId(correlationId);

        // Add to response headers
        await context.WriteResponseHeadersAsync(new Metadata
        {
            { CorrelationIdMetadataKey, correlationId }
        });

        return await continuation(request, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var correlationId = GetOrCreateCorrelationId(context.RequestHeaders);
        DefaultCorrelationIdAccessor.SetCorrelationId(correlationId);

        await context.WriteResponseHeadersAsync(new Metadata
        {
            { CorrelationIdMetadataKey, correlationId }
        });

        return await continuation(requestStream, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var correlationId = GetOrCreateCorrelationId(context.RequestHeaders);
        DefaultCorrelationIdAccessor.SetCorrelationId(correlationId);

        await context.WriteResponseHeadersAsync(new Metadata
        {
            { CorrelationIdMetadataKey, correlationId }
        });

        await continuation(request, responseStream, context);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var correlationId = GetOrCreateCorrelationId(context.RequestHeaders);
        DefaultCorrelationIdAccessor.SetCorrelationId(correlationId);

        await context.WriteResponseHeadersAsync(new Metadata
        {
            { CorrelationIdMetadataKey, correlationId }
        });

        await continuation(requestStream, responseStream, context);
    }

    private static string GetOrCreateCorrelationId(Metadata headers)
    {
        var correlationIdEntry = headers.FirstOrDefault(m => 
            m.Key.Equals(CorrelationIdMetadataKey, StringComparison.OrdinalIgnoreCase));

        if (correlationIdEntry != null && !string.IsNullOrEmpty(correlationIdEntry.Value))
        {
            return correlationIdEntry.Value;
        }

        // Try alternative headers
        var requestIdEntry = headers.FirstOrDefault(m => 
            m.Key.Equals("x-request-id", StringComparison.OrdinalIgnoreCase));

        if (requestIdEntry != null && !string.IsNullOrEmpty(requestIdEntry.Value))
        {
            return requestIdEntry.Value;
        }

        return Guid.NewGuid().ToString();
    }
}
