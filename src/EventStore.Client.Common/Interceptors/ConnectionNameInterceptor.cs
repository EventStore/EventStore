using System;
using Grpc.Core;
using Grpc.Core.Interceptors;

// ReSharper disable CheckNamespace
namespace EventStore.Client.Interceptors {
	// ReSharper restore CheckNamespace
	internal class ConnectionNameInterceptor : Interceptor {
		private readonly string _connectionName;

		public ConnectionNameInterceptor(string connectionName) {
			if (connectionName == null) throw new ArgumentNullException(nameof(connectionName));
			_connectionName = connectionName;
		}

		public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request,
			ClientInterceptorContext<TRequest, TResponse> context,
			AsyncUnaryCallContinuation<TRequest, TResponse> continuation) {
			AddConnectionName(context);
			return continuation(request, context);
		}

		public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
			ClientInterceptorContext<TRequest, TResponse> context,
			AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation) {
			AddConnectionName(context);
			return continuation(context);
		}

		public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
			TRequest request,
			ClientInterceptorContext<TRequest, TResponse> context,
			AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation) {
			AddConnectionName(context);
			return continuation(request, context);
		}

		public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
			ClientInterceptorContext<TRequest, TResponse> context,
			AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation) {
			AddConnectionName(context);
			return continuation(context);
		}

		private void AddConnectionName<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context)
			where TRequest : class where TResponse : class =>
			context.Options.Headers.Add(Constants.Headers.ConnectionName, _connectionName);
	}
}
