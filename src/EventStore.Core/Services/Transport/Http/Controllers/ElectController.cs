using System;
using System.Net;
using System.Net.Http;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Client;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using HttpMethod = EventStore.Transport.Http.HttpMethod;

namespace EventStore.Core.Services.Transport.Http.Controllers {
	public class ElectController : CommunicationController,
		IHttpSender,
		ISender<ElectionMessage.ViewChange>,
		ISender<ElectionMessage.ViewChangeProof>,
		ISender<ElectionMessage.Prepare>,
		ISender<ElectionMessage.PrepareOk>,
		ISender<ElectionMessage.Proposal>,
		ISender<ElectionMessage.Accept>,
		ISender<ElectionMessage.MasterIsResigning>,
		ISender<ElectionMessage.MasterIsResigningOk>
		{
		private static readonly ILogger Log = LogManager.GetLoggerFor<ElectController>();
		private static readonly ICodec[] SupportedCodecs = new ICodec[] {Codec.Json, Codec.Xml};
		private TimeSpan _operationTimeout;
		private readonly HttpAsyncClient _client;

		public ElectController(IPublisher publisher, HttpMessageHandler httpMessageHandler) : base(publisher) {
			_operationTimeout = TimeSpan.FromMilliseconds(2000); //TODO make these configurable
			_client = new HttpAsyncClient(_operationTimeout, httpMessageHandler);
		}

		protected override void SubscribeCore(IHttpService service) {
			service.RegisterAction(
				new ControllerAction("/elections/viewchange", HttpMethod.Post, SupportedCodecs, SupportedCodecs, AuthorizationLevel.None),
				OnPostViewChange);
			service.RegisterAction(
				new ControllerAction("/elections/viewchangeproof", HttpMethod.Post, SupportedCodecs, SupportedCodecs, AuthorizationLevel.None),
				OnPostViewChangeProof);
			service.RegisterAction(
				new ControllerAction("/elections/prepare", HttpMethod.Post, SupportedCodecs, SupportedCodecs, AuthorizationLevel.None),
				OnPostPrepare);
			service.RegisterAction(
				new ControllerAction("/elections/prepareok", HttpMethod.Post, SupportedCodecs, SupportedCodecs, AuthorizationLevel.None),
				OnPostPrepareOk);
			service.RegisterAction(
				new ControllerAction("/elections/proposal", HttpMethod.Post, SupportedCodecs, SupportedCodecs, AuthorizationLevel.None),
				OnPostProposal);
			service.RegisterAction(
				new ControllerAction("/elections/accept", HttpMethod.Post, SupportedCodecs, SupportedCodecs, AuthorizationLevel.None),
				OnPostAccept);
			service.RegisterAction(
				new ControllerAction("/elections/masterisresigning", HttpMethod.Post, SupportedCodecs, SupportedCodecs, AuthorizationLevel.None),
				OnPostMasterIsResigning);
			service.RegisterAction(
				new ControllerAction("/elections/masterisresigningok", HttpMethod.Post, SupportedCodecs, SupportedCodecs, AuthorizationLevel.None),
				OnPostMasterIsResigningOk);
		}

		public void SubscribeSenders(HttpMessagePipe pipe) {
			pipe.RegisterSender<ElectionMessage.ViewChange>(this);
			pipe.RegisterSender<ElectionMessage.ViewChangeProof>(this);
			pipe.RegisterSender<ElectionMessage.Prepare>(this);
			pipe.RegisterSender<ElectionMessage.PrepareOk>(this);
			pipe.RegisterSender<ElectionMessage.Proposal>(this);
			pipe.RegisterSender<ElectionMessage.Accept>(this);
			pipe.RegisterSender<ElectionMessage.MasterIsResigning>(this);
			pipe.RegisterSender<ElectionMessage.MasterIsResigningOk>(this);
		}

		public void Send(ElectionMessage.ViewChange message, IPEndPoint endPoint) {
			Ensure.NotNull(message, "message");
			Ensure.NotNull(endPoint, "endPoint");

			_client.Post(endPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA, "/elections/viewchange"),
				Codec.Json.To(new ElectionMessageDto.ViewChangeDto(message)),
				Codec.Json.ContentType,
				r => {
					/*ignore*/
				},
				e => {
					/*Log.ErrorException(e, "Error occured while writing request (elections/viewchange)")*/
				});
		}

		public void Send(ElectionMessage.ViewChangeProof message, IPEndPoint endPoint) {
			Ensure.NotNull(message, "message");
			Ensure.NotNull(endPoint, "endPoint");

			_client.Post(endPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA, "/elections/viewchangeproof"),
				Codec.Json.To(new ElectionMessageDto.ViewChangeProofDto(message)),
				Codec.Json.ContentType,
				r => {
					/*ignore*/
				},
				e => {
					/*Log.ErrorException(e, "Error occured while writing request (elections/viewchangeproof)")*/
				});
		}

		public void Send(ElectionMessage.Prepare message, IPEndPoint endPoint) {
			Ensure.NotNull(message, "message");
			Ensure.NotNull(endPoint, "endPoint");

			_client.Post(endPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA, "/elections/prepare"),
				Codec.Json.To(new ElectionMessageDto.PrepareDto(message)),
				Codec.Json.ContentType,
				r => {
					/*ignore*/
				},
				e => {
					/*Log.ErrorException(e, "Error occured while writing request (elections/prepare)")*/
				});
		}

		public void Send(ElectionMessage.PrepareOk message, IPEndPoint endPoint) {
			Ensure.NotNull(message, "message");
			Ensure.NotNull(endPoint, "endPoint");

			_client.Post(endPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA, "/elections/prepareok"),
				Codec.Json.To(new ElectionMessageDto.PrepareOkDto(message)),
				Codec.Json.ContentType,
				r => {
					/*ignore*/
				},
				e => {
					/*Log.ErrorException(e, "Error occured while writing request (elections/prepareok)")*/
				});
		}

		public void Send(ElectionMessage.Proposal message, IPEndPoint endPoint) {
			Ensure.NotNull(message, "message");
			Ensure.NotNull(endPoint, "endPoint");

			_client.Post(endPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA, "/elections/proposal"),
				Codec.Json.To(new ElectionMessageDto.ProposalDto(message)),
				Codec.Json.ContentType,
				r => {
					/*ignore*/
				},
				e => {
					/*Log.ErrorException(e, "Error occured while writing request (elections/proposal)")*/
				});
		}

		public void Send(ElectionMessage.Accept message, IPEndPoint endPoint) {
			Ensure.NotNull(message, "message");
			Ensure.NotNull(endPoint, "endPoint");

			_client.Post(endPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA, "/elections/accept"),
				Codec.Json.To(new ElectionMessageDto.AcceptDto(message)),
				Codec.Json.ContentType,
				r => {
					/*ignore*/
				},
				e => {
					/*Log.ErrorException(e, "Error occured while writing request (elections/accept)")*/
				});
		}
		
		public void Send(ElectionMessage.MasterIsResigning message, IPEndPoint endPoint) {
			Ensure.NotNull(message, nameof(message));
			Ensure.NotNull(endPoint, nameof(endPoint));

			_client.Post(endPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA, "/elections/masterisresigning"),
				Codec.Json.To(new ElectionMessageDto.MasterIsResigningDto(message)),
				Codec.Json.ContentType,
				r => {
					/*ignore*/
				},
				e => {
					/*Log.ErrorException(e, "Error occured while writing request (elections/masterisresigning)")*/
				});
		}
		
		public void Send(ElectionMessage.MasterIsResigningOk message, IPEndPoint endPoint) {
			Ensure.NotNull(message, nameof(message));
			Ensure.NotNull(endPoint, nameof(endPoint));

			_client.Post(endPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA, "/elections/masterisresigningok"),
				Codec.Json.To(new ElectionMessageDto.MasterIsResigningOkDto(message)),
				Codec.Json.ContentType,
				r => {
					/*ignore*/
				},
				e => {
					/*Log.ErrorException(e, "Error occured while writing request (elections/masterisresigningok)")*/
				});
		}

		private void OnPost<TDto, TMessage>(HttpEntityManager manager, Func<TDto, TMessage> unwrapper)
			where TDto : class
			where TMessage : Message {
			manager.AsyncState = new PostState(body => {
				var dto = manager.RequestCodec.From<TDto>(body);
				return dto != null ? unwrapper(dto) : null;
			});
			manager.ReadTextRequestAsync(OnPostRequestRead,
				e => Log.Debug("Error while reading request: {e}.", e.Message));
		}

		private void OnPostRequestRead(HttpEntityManager manager, string body) {
			var state = (PostState)manager.AsyncState;
			var message = state.Unwrapper(body);

			if (message != null) {
				Publish(message);
				SendOk(manager);
			} else
				SendBadRequest(manager, string.Format("Invalid request. Body contains badly formatted object"));
		}

		private void OnPostViewChange(HttpEntityManager manager, UriTemplateMatch match) {
			OnPost(manager, (ElectionMessageDto.ViewChangeDto dto) => new ElectionMessage.ViewChange(dto));
		}

		private void OnPostViewChangeProof(HttpEntityManager manager, UriTemplateMatch match) {
			OnPost(manager, (ElectionMessageDto.ViewChangeProofDto dto) => new ElectionMessage.ViewChangeProof(dto));
		}

		private void OnPostPrepare(HttpEntityManager manager, UriTemplateMatch match) {
			OnPost(manager, (ElectionMessageDto.PrepareDto dto) => new ElectionMessage.Prepare(dto));
		}

		private void OnPostPrepareOk(HttpEntityManager manager, UriTemplateMatch match) {
			OnPost(manager, (ElectionMessageDto.PrepareOkDto dto) => new ElectionMessage.PrepareOk(dto));
		}

		private void OnPostProposal(HttpEntityManager manager, UriTemplateMatch match) {
			OnPost(manager, (ElectionMessageDto.ProposalDto dto) => new ElectionMessage.Proposal(dto));
		}

		private void OnPostAccept(HttpEntityManager manager, UriTemplateMatch match) {
			OnPost(manager, (ElectionMessageDto.AcceptDto dto) => new ElectionMessage.Accept(dto));
		}
		
		private void OnPostMasterIsResigning(HttpEntityManager manager, UriTemplateMatch match) {
			OnPost(manager, (ElectionMessageDto.MasterIsResigningDto dto) => new ElectionMessage.MasterIsResigning(dto));
		}
		
		private void OnPostMasterIsResigningOk(HttpEntityManager manager, UriTemplateMatch match) {
			OnPost(manager, (ElectionMessageDto.MasterIsResigningOkDto dto) => new ElectionMessage.MasterIsResigningOk(dto));
		}

		private class PostState {
			public readonly Func<string, Message> Unwrapper;

			public PostState(Func<string, Message> unwrapper) {
				Ensure.NotNull(unwrapper, "unwrapper");
				Unwrapper = unwrapper;
			}
		}
	}
}
