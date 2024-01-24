using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Scanning;

namespace EventStore.Core.Tests;

public class MessageHierarchyCatalogTests {
	[Fact]
	public void Works() {
		//var temp = MessageHierarchyResolver.Descendants[typeof(ClientMessage.RequestShutdown)];
			
		var assemblies = new DependencyContextAssemblyCatalog().GetAssemblies();
			
		var messageTypes = MessagesAssemblyScanner
			.FindMessagesInAssemblies(assemblies)
			.Select(x => x.MessageType)
			.ToList();
			
		var catalog  = MessageHierarchyCatalog.Build(messageTypes);
			
		//var descendants = catalog.DescendantsOf<ClientMessage.RequestShutdown>();
		var ascendants  = catalog.AscendantsOf<ClientMessage.RequestShutdown>();
			
		Assert.True(true);
	}
}
