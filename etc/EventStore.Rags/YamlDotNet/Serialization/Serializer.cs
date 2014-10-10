//  This file is part of YamlDotNet - A .NET library for YAML.
//  Copyright (c) 2008, 2009, 2010, 2011, 2012, 2013 Antoine Aubry

//  Permission is hereby granted, free of charge, to any person obtaining a copy of
//  this software and associated documentation files (the "Software"), to deal in
//  the Software without restriction, including without limitation the rights to
//  use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
//  of the Software, and to permit persons to whom the Software is furnished to do
//  so, subject to the following conditions:

//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.

//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using EventStore.Rags.YamlDotNet.Core;
using EventStore.Rags.YamlDotNet.Core.Events;
using EventStore.Rags.YamlDotNet.Serialization.EventEmitters;
using EventStore.Rags.YamlDotNet.Serialization.NamingConventions;
using EventStore.Rags.YamlDotNet.Serialization.ObjectGraphTraversalStrategies;
using EventStore.Rags.YamlDotNet.Serialization.ObjectGraphVisitors;
using EventStore.Rags.YamlDotNet.Serialization.TypeInspectors;
using EventStore.Rags.YamlDotNet.Serialization.TypeResolvers;

namespace EventStore.Rags.YamlDotNet.Serialization
{
	/// <summary>
	/// Writes objects to YAML.
	/// </summary>
	public sealed class Serializer
	{
		internal IList<IYamlTypeConverter> Converters { get; private set; }

		private readonly SerializationOptions options;
		private readonly INamingConvention namingConvention;
		private readonly ITypeResolver typeResolver;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="options">Options that control how the serialization is to be performed.</param>
		/// <param name="namingConvention">Naming strategy to use for serialized property names</param>
		public Serializer(SerializationOptions options = SerializationOptions.None, INamingConvention namingConvention = null)
		{
			this.options = options;
			this.namingConvention = namingConvention ?? new NullNamingConvention();

			Converters = new List<IYamlTypeConverter>();

			typeResolver = IsOptionSet(SerializationOptions.DefaultToStaticType)
				? (ITypeResolver)new StaticTypeResolver()
				: (ITypeResolver)new DynamicTypeResolver();
		}

		private bool IsOptionSet(SerializationOptions option)
		{
			return (options & option) != 0;
		}

		/// <summary>
		/// Registers a type converter to be used to serialize and deserialize specific types.
		/// </summary>
		public void RegisterTypeConverter(IYamlTypeConverter converter)
		{
			Converters.Add(converter);
		}

		/// <summary>
		/// Serializes the specified object.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter" /> where to serialize the object.</param>
		/// <param name="graph">The object to serialize.</param>
		public void Serialize(TextWriter writer, object graph)
		{
			Serialize(new Emitter(writer), graph);
		}

		/// <summary>
		/// Serializes the specified object.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter" /> where to serialize the object.</param>
		/// <param name="graph">The object to serialize.</param>
		/// <param name="type">The static type of the object to serialize.</param>
		public void Serialize(TextWriter writer, object graph, Type type)
		{
			Serialize(new Emitter(writer), graph, type);
		}

		/// <summary>
		/// Serializes the specified object.
		/// </summary>
		/// <param name="emitter">The <see cref="IEmitter" /> where to serialize the object.</param>
		/// <param name="graph">The object to serialize.</param>
		public void Serialize(IEmitter emitter, object graph)
		{
			if (emitter == null)
			{
				throw new ArgumentNullException("emitter");
			}

			EmitDocument(emitter, new ObjectDescriptor(graph, graph != null ? graph.GetType() : typeof(object), typeof(object)));
		}

		/// <summary>
		/// Serializes the specified object.
		/// </summary>
		/// <param name="emitter">The <see cref="IEmitter" /> where to serialize the object.</param>
		/// <param name="graph">The object to serialize.</param>
		/// <param name="type">The static type of the object to serialize.</param>
		public void Serialize(IEmitter emitter, object graph, Type type)
		{
			if (emitter == null)
			{
				throw new ArgumentNullException("emitter");
			}

			if (type == null)
			{
				throw new ArgumentNullException("type");
			}

			EmitDocument(emitter, new ObjectDescriptor(graph, type, type));
		}

		private void EmitDocument(IEmitter emitter, IObjectDescriptor graph)
		{
			var traversalStrategy = CreateTraversalStrategy();
			var eventEmitter = CreateEventEmitter(emitter);
			var emittingVisitor = CreateEmittingVisitor(emitter, traversalStrategy, eventEmitter, graph);

			emitter.Emit(new StreamStart());
			emitter.Emit(new DocumentStart());

			traversalStrategy.Traverse(graph, emittingVisitor);

			emitter.Emit(new DocumentEnd(true));
			emitter.Emit(new StreamEnd());
		}

		private IObjectGraphVisitor CreateEmittingVisitor(IEmitter emitter, IObjectGraphTraversalStrategy traversalStrategy, IEventEmitter eventEmitter, IObjectDescriptor graph)
		{
			IObjectGraphVisitor emittingVisitor = new EmittingObjectGraphVisitor(eventEmitter);

			emittingVisitor = new CustomSerializationObjectGraphVisitor(emitter, emittingVisitor, Converters);

			if (!IsOptionSet(SerializationOptions.DisableAliases))
			{
				var anchorAssigner = new AnchorAssigner();
				traversalStrategy.Traverse(graph, anchorAssigner);

				emittingVisitor = new AnchorAssigningObjectGraphVisitor(emittingVisitor, eventEmitter, anchorAssigner);
			}

			if (!IsOptionSet(SerializationOptions.EmitDefaults))
			{
				emittingVisitor = new DefaultExclusiveObjectGraphVisitor(emittingVisitor);
			}

			return emittingVisitor;
		}

		private IEventEmitter CreateEventEmitter(IEmitter emitter)
		{
			var writer = new WriterEventEmitter(emitter);

			if (IsOptionSet(SerializationOptions.JsonCompatible))
			{
				return new JsonEventEmitter(writer);
			}
			else
			{
				return new TypeAssigningEventEmitter(writer, IsOptionSet(SerializationOptions.Roundtrip));
			}
		}

		private IObjectGraphTraversalStrategy CreateTraversalStrategy()
		{
			ITypeInspector typeDescriptor = new ReadablePropertiesTypeInspector(typeResolver);
			if (IsOptionSet(SerializationOptions.Roundtrip))
			{
				typeDescriptor = new ReadableAndWritablePropertiesTypeInspector(typeDescriptor);
			}

			typeDescriptor = new NamingConventionTypeInspector(typeDescriptor, namingConvention);
			typeDescriptor = new YamlAttributesTypeInspector(typeDescriptor);

			if (IsOptionSet(SerializationOptions.Roundtrip))
			{
				return new RoundtripObjectGraphTraversalStrategy(this, typeDescriptor, typeResolver, 50);
			}
			else
			{
				return new FullObjectGraphTraversalStrategy(this, typeDescriptor, typeResolver, 50);
			}
		}
	}
}