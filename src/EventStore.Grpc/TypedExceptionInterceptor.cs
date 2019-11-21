using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace EventStore.Grpc {
	public class TypedExceptionInterceptor : Interceptor {
		public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
			TRequest request,
			ClientInterceptorContext<TRequest, TResponse> context,
			AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation) {
			var response = continuation(request, context);

			return new AsyncServerStreamingCall<TResponse>(new AsyncStreamReader<TResponse>(response.ResponseStream),
				response.ResponseHeadersAsync, response.GetStatus, response.GetTrailers, response.Dispose);
		}

		public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
			ClientInterceptorContext<TRequest, TResponse> context,
			AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation) {
			var response = continuation(context);

			return new AsyncClientStreamingCall<TRequest, TResponse>(
				response.RequestStream,
				response.ResponseAsync.ContinueWith(t => {
					if (t.Exception?.InnerException is RpcException ex) {
						throw ConvertRpcException(ex);
					}

					return t.Result;
				}),
				response.ResponseHeadersAsync,
				response.GetStatus,
				response.GetTrailers,
				response.Dispose);
		}

		public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
			TRequest request,
			ClientInterceptorContext<TRequest, TResponse> context,
			AsyncUnaryCallContinuation<TRequest, TResponse> continuation) {
			var response = continuation(request, context);

			return new AsyncUnaryCall<TResponse>(response.ResponseAsync.ContinueWith(t => {
				if (t.Exception?.InnerException is RpcException ex) {
					throw ConvertRpcException(ex);
				}

				return t.Result;
			}), response.ResponseHeadersAsync, response.GetStatus, response.GetTrailers, response.Dispose);
		}


		static Exception ConvertRpcException(RpcException ex)
			=> ex.Trailers.TryGetValue(Constants.Exceptions.ExceptionKey, out var value) switch {
				true => value switch {
					Constants.Exceptions.AccessDenied => new AccessDeniedException(ex.Message, ex),
					Constants.Exceptions.InvalidTransaction => new InvalidTransactionException(ex.Message, ex),
					Constants.Exceptions.StreamDeleted => new StreamDeletedException(
						ex.Trailers.FirstOrDefault(x => x.Key == Constants.Exceptions.StreamName)?.Value ?? "<unknown>",
						ex),
					Constants.Exceptions.WrongExpectedVersion => new WrongExpectedVersionException(
						ex.Trailers.FirstOrDefault(x => x.Key == Constants.Exceptions.StreamName)?.Value,
						ex.Trailers.GetLongValueOrDefault(Constants.Exceptions.ExpectedVersion),
						ex.Trailers.GetLongValueOrDefault(Constants.Exceptions.ActualVersion),
						ex),
					Constants.Exceptions.NotFound => new StreamNotFoundException(
						ex.Trailers.FirstOrDefault(x => x.Key == Constants.Exceptions.StreamName)?.Value, ex),
					_ => (Exception)new InvalidOperationException(ex.Message, ex)
				},
				false => new InvalidOperationException(ex.Message, ex)
			};

		class AsyncStreamReader<TResponse> : IAsyncStreamReader<TResponse> {
			private readonly IAsyncStreamReader<TResponse> _inner;

			public AsyncStreamReader(IAsyncStreamReader<TResponse> inner) {
				if (inner == null) throw new ArgumentNullException(nameof(inner));
				_inner = inner;
			}

			public async Task<bool> MoveNext(CancellationToken cancellationToken) {
				try {
					return await _inner.MoveNext(cancellationToken);
				} catch (RpcException ex) {
					throw ConvertRpcException(ex);
				}
			}

			public TResponse Current => _inner.Current;
		}
	}
}
