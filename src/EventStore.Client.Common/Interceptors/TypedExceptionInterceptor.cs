using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.PersistentSubscriptions;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace EventStore.Client {
	internal class TypedExceptionInterceptor : Interceptor {
		private readonly Action<Exception> _exceptionOccurred;

		public TypedExceptionInterceptor(Action<Exception> exceptionOccurred = null) {
			_exceptionOccurred = exceptionOccurred;
		}

		public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
			TRequest request,
			ClientInterceptorContext<TRequest, TResponse> context,
			AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation) {
			var response = continuation(request, context);

			return new AsyncServerStreamingCall<TResponse>(
				new AsyncStreamReader<TResponse>(_exceptionOccurred, response.ResponseStream),
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
						var exception = ConvertRpcException(ex);
						_exceptionOccurred?.Invoke(exception);
						throw exception;
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
					var exception = ConvertRpcException(ex);
					_exceptionOccurred?.Invoke(exception);
					throw exception;
				}

				return t.Result;
			}), response.ResponseHeadersAsync, response.GetStatus, response.GetTrailers, response.Dispose);
		}

		public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
			ClientInterceptorContext<TRequest, TResponse> context,
			AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation) {
			var response = continuation(context);

			return new AsyncDuplexStreamingCall<TRequest, TResponse>(
				response.RequestStream,
				new AsyncStreamReader<TResponse>(_exceptionOccurred, response.ResponseStream),
				response.ResponseHeadersAsync,
				response.GetStatus,
				response.GetTrailers,
				response.Dispose);
		}

		private static Exception ConvertRpcException(RpcException ex) {
			return ex.Trailers.TryGetValue(Constants.Exceptions.ExceptionKey, out var value) switch {
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
					Constants.Exceptions.MaximumAppendSizeExceeded => new MaximumAppendSizeExceededException(
						ex.Trailers.GetIntValueOrDefault(Constants.Exceptions.MaximumAppendSize), ex),
					Constants.Exceptions.StreamNotFound => new StreamNotFoundException(
						ex.Trailers.FirstOrDefault(x => x.Key == Constants.Exceptions.StreamName)?.Value, ex),
					Constants.Exceptions.PersistentSubscriptionDoesNotExist => new
						PersistentSubscriptionNotFoundException(
							ex.Trailers.FirstOrDefault(x => x.Key == Constants.Exceptions.StreamName)?.Value,
							ex.Trailers.FirstOrDefault(x => x.Key == Constants.Exceptions.GroupName)?.Value, ex),
					Constants.Exceptions.MaximumSubscribersReached => new
						MaximumSubscribersReachedException(
							ex.Trailers.FirstOrDefault(x => x.Key == Constants.Exceptions.StreamName)?.Value,
							ex.Trailers.FirstOrDefault(x => x.Key == Constants.Exceptions.GroupName)?.Value, ex),
					Constants.Exceptions.PersistentSubscriptionDropped => new
						PersistentSubscriptionDroppedByServerException(
							ex.Trailers.FirstOrDefault(x => x.Key == Constants.Exceptions.StreamName)?.Value,
							ex.Trailers.FirstOrDefault(x => x.Key == Constants.Exceptions.GroupName)?.Value, ex),
					Constants.Exceptions.UserNotFound => new UserNotFoundException(
						ex.Trailers.FirstOrDefault(x => x.Key == Constants.Exceptions.LoginName)?.Value),
					Constants.Exceptions.MissingRequiredMetadataProperty => new
						RequiredMetadataPropertyMissingException(
							ex.Trailers.FirstOrDefault(x =>
									x.Key == Constants.Exceptions.MissingRequiredMetadataProperty)
								?.Value, ex),
					Constants.Exceptions.ScavengeNotFound => new ScavengeNotFoundException(ex.Trailers
						.FirstOrDefault(x => x.Key == Constants.Exceptions.ScavengeId)?.Value),
					Constants.Exceptions.NotLeader => new NotLeaderException(IPEndPoint.Parse(ex.Trailers
						.FirstOrDefault(x => x.Key == Constants.Exceptions.LeaderEndpoint)?.Value)),
					_ => (Exception)new InvalidOperationException(ex.Message, ex)
				},
				false => ex.StatusCode switch {
					StatusCode.DeadlineExceeded => new TimeoutException(ex.Message, ex),
					StatusCode.Unauthenticated => new NotAuthenticatedException(ex.Message, ex),
					_ => new InvalidOperationException(ex.Message, ex)
				}
			};
		}

		class AsyncStreamReader<TResponse> : IAsyncStreamReader<TResponse> {
			private readonly Action<Exception> _exceptionOccurred;
			private readonly IAsyncStreamReader<TResponse> _inner;

			public AsyncStreamReader(Action<Exception> exceptionOccurred, IAsyncStreamReader<TResponse> inner) {
				if (inner == null) throw new ArgumentNullException(nameof(inner));
				_exceptionOccurred = exceptionOccurred;
				_inner = inner;
			}

			public async Task<bool> MoveNext(CancellationToken cancellationToken) {
				try {
					return await _inner.MoveNext(cancellationToken).ConfigureAwait(false);
				} catch (RpcException ex) {
					var exception = ConvertRpcException(ex);
					_exceptionOccurred?.Invoke(exception);
					throw exception;
				}
			}

			public TResponse Current => _inner.Current;
		}
	}
}
