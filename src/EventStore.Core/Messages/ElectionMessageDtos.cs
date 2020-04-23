using System;

namespace EventStore.Core.Messages {
	public static class ElectionMessageDto {
		public class ViewChangeDto {
			public Guid ServerId { get; set; }
			public string ServerInternalHttpAddress { get; set; }
			public int ServerInternalHttpPort { get; set; }

			public int AttemptedView { get; set; }

			public ViewChangeDto() {
			}

			public ViewChangeDto(ElectionMessage.ViewChange message) {
				ServerId = message.ServerId;
				ServerInternalHttpAddress = message.ServerInternalHttp.Address.ToString();
				ServerInternalHttpPort = message.ServerInternalHttp.Port;

				AttemptedView = message.AttemptedView;
			}
		}

		public class ViewChangeProofDto {
			public Guid ServerId { get; set; }
			public string ServerInternalHttpAddress { get; set; }
			public int ServerInternalHttpPort { get; set; }

			public int InstalledView { get; set; }

			public ViewChangeProofDto() {
			}

			public ViewChangeProofDto(ElectionMessage.ViewChangeProof message) {
				ServerId = message.ServerId;
				ServerInternalHttpAddress = message.ServerInternalHttp.Address.ToString();
				ServerInternalHttpPort = message.ServerInternalHttp.Port;

				InstalledView = message.InstalledView;
			}
		}

		public class PrepareDto {
			public Guid ServerId { get; set; }
			public string ServerInternalHttpAddress { get; set; }
			public int ServerInternalHttpPort { get; set; }

			public int View { get; set; }

			public PrepareDto() {
			}

			public PrepareDto(ElectionMessage.Prepare message) {
				ServerId = message.ServerId;
				ServerInternalHttpAddress = message.ServerInternalHttp.Address.ToString();
				ServerInternalHttpPort = message.ServerInternalHttp.Port;

				View = message.View;
			}
		}

		public class PrepareOkDto {
			public Guid ServerId { get; set; }
			public string ServerInternalHttpAddress { get; set; }
			public int ServerInternalHttpPort { get; set; }

			public int View { get; set; }

			public int EpochNumber { get; set; }
			public long EpochPosition { get; set; }
			public Guid EpochId { get; set; }
			public long LastCommitPosition { get; set; }
			public long WriterCheckpoint { get; set; }
			public long ChaserCheckpoint { get; set; }

			public int NodePriority { get; set; }

			public PrepareOkDto() {
			}

			public PrepareOkDto(ElectionMessage.PrepareOk message) {
				ServerId = message.ServerId;
				ServerInternalHttpAddress = message.ServerInternalHttp.Address.ToString();
				ServerInternalHttpPort = message.ServerInternalHttp.Port;

				View = message.View;

				EpochNumber = message.EpochNumber;
				EpochPosition = message.EpochPosition;
				EpochId = message.EpochId;
				LastCommitPosition = message.LastCommitPosition;
				WriterCheckpoint = message.WriterCheckpoint;
				ChaserCheckpoint = message.ChaserCheckpoint;

				NodePriority = message.NodePriority;
			}
		}


		public class ProposalDto {
			public Guid ServerId { get; set; }
			public Guid LeaderId { get; set; }

			public string ServerInternalHttpAddress { get; set; }
			public int ServerInternalHttpPort { get; set; }
			public string LeaderInternalHttpAddress { get; set; }
			public int LeaderInternalHttpPort { get; set; }

			public int View { get; set; }

			public long LastCommitPosition { get; set; }
			public long WriterCheckpoint { get; set; }
			public long ChaserCheckpoint { get; set; }
			public int EpochNumber { get; set; }
			public long EpochPosition { get; set; }
			public Guid EpochId { get; set; }
			public int NodePriority { get; set; }

			public ProposalDto() {
			}

			public ProposalDto(ElectionMessage.Proposal message) {
				ServerId = message.ServerId;
				LeaderId = message.LeaderId;

				ServerInternalHttpAddress = message.ServerInternalHttp.Address.ToString();
				ServerInternalHttpPort = message.ServerInternalHttp.Port;
				LeaderInternalHttpAddress = message.LeaderInternalHttp.Address.ToString();
				LeaderInternalHttpPort = message.LeaderInternalHttp.Port;

				View = message.View;
				EpochNumber = message.EpochNumber;
				EpochPosition = message.EpochPosition;
				EpochId = message.EpochId;
				LastCommitPosition = message.LastCommitPosition;
				WriterCheckpoint = message.WriterCheckpoint;
				ChaserCheckpoint = message.ChaserCheckpoint;
				NodePriority = message.NodePriority;
			}
		}


		public class AcceptDto {
			public Guid ServerId { get; set; }
			public Guid LeaderId { get; set; }

			public string ServerInternalHttpAddress { get; set; }
			public int ServerInternalHttpPort { get; set; }
			public string LeaderInternalHttpAddress { get; set; }
			public int LeaderInternalHttpPort { get; set; }

			public int View { get; set; }

			public AcceptDto() {
			}

			public AcceptDto(ElectionMessage.Accept message) {
				ServerId = message.ServerId;
				LeaderId = message.LeaderId;

				ServerInternalHttpAddress = message.ServerInternalHttp.Address.ToString();
				ServerInternalHttpPort = message.ServerInternalHttp.Port;
				LeaderInternalHttpAddress = message.LeaderInternalHttp.Address.ToString();
				LeaderInternalHttpPort = message.LeaderInternalHttp.Port;

				View = message.View;
			}
		}
		
		public class LeaderIsResigningDto {
			public Guid LeaderId { get; set; }
			public string LeaderInternalHttpAddress { get; set; }
			public int LeaderInternalHttpPort { get; set; }
			public LeaderIsResigningDto() {
			}

			public LeaderIsResigningDto(ElectionMessage.LeaderIsResigning message) {
				LeaderId = message.LeaderId;
				LeaderInternalHttpAddress = message.LeaderInternalHttp.Address.ToString();
				LeaderInternalHttpPort = message.LeaderInternalHttp.Port;
			}
		}
		
		public class LeaderIsResigningOkDto {
			public Guid LeaderId { get; set; }
			public string LeaderInternalHttpAddress { get; set; }
			public int LeaderInternalHttpPort { get; set; }
			public Guid ServerId { get; set; }
			public string ServerInternalHttpAddress { get; set; }
			public int ServerInternalHttpPort { get; set; }
			public LeaderIsResigningOkDto() {
			}

			public LeaderIsResigningOkDto(ElectionMessage.LeaderIsResigningOk message) {
				ServerId = message.ServerId;
				ServerInternalHttpAddress = message.ServerInternalHttp.Address.ToString();
				ServerInternalHttpPort = message.ServerInternalHttp.Port;
				LeaderId = message.LeaderId;
				LeaderInternalHttpAddress = message.LeaderInternalHttp.Address.ToString();
				LeaderInternalHttpPort = message.LeaderInternalHttp.Port;
			}
		}
	}
}
