using System;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Core.Transforms;

public class DbTransformManager {
	private readonly IReadOnlyList<IDbTransform> _transforms;
	private IDbTransform _activeTransform;

	public DbTransformManager(IReadOnlyList<IDbTransform> transforms, TransformType activeTransformType) {
		_transforms = transforms;
		_activeTransform = FindTransform(activeTransformType);

		// the identity transform is always required
		_ = FindTransform(TransformType.Identity);
	}

	private IDbTransform FindTransform(TransformType type) =>
		_transforms.FirstOrDefault(t => t.Type == type) ??
		       throw new Exception($"Failed to load transform: {type}");

	public IChunkTransformFactory GetFactoryForNewChunk() => _activeTransform.ChunkFactory;
	public IChunkTransformFactory GetFactoryForExistingChunk(TransformType type) => FindTransform(type).ChunkFactory;

	public void SetActiveTransform(TransformType type) {
		_activeTransform = FindTransform(type);
	}

	public bool SupportsTransform(TransformType type) {
		try {
			FindTransform(type);
			return true;
		} catch {
			return false;
		}
	}
}
