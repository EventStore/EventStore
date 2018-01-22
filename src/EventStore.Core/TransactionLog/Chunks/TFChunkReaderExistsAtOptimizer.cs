using System;
using EventStore.Common.Log;
using EventStore.Core.DataStructures;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkReaderExistsAtOptimizer
    {
        private static TFChunkReaderExistsAtOptimizer _instance;
        public static TFChunkReaderExistsAtOptimizer Instance {
            get
            {
                if (_instance == null)
                {
                    _instance = new TFChunkReaderExistsAtOptimizer(MaxBloomFiltersCached);
                }
                return _instance;
            }
        }

        //Least-Recently-Used cache to keep track of scavenged TFChunks that have cached bloom filters
        private LRUCache<string, TFChunk.TFChunk> _cache;
        private const int MaxBloomFiltersCached = 10000; //around 5GB RAM max if we consider 200,000 log positions/chunk and 20 bits/log position
        private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunkReaderExistsAtOptimizer>();


        public TFChunkReaderExistsAtOptimizer(int maxCached){
            Func<object, bool> onPut = (o) => {
                var chunk = (TFChunk.TFChunk) o;
                if(chunk == null)
                    return false;
                Log.Debug("Optimizing chunk "+chunk.FileName+" for fast merge...");
                chunk.OptimizeExistsAt();
                return true;
            };

            Func<object, bool> onRemove = (o) => {
                var chunk = (TFChunk.TFChunk) o;
                if(chunk == null)
                    return false;
                Log.Debug("Clearing fast merge optimizations from chunk "+chunk.FileName+"...");
                chunk.DeOptimizeExistsAt();
                return true;
            };

            _cache = new LRUCache<string, TFChunk.TFChunk>(maxCached, onPut, onRemove);
        }

        public void Optimize(TFChunk.TFChunk chunk){
            if(!chunk.ChunkHeader.IsScavenged) return;
            _cache.Put(chunk.FileName, chunk);
        }

        public bool IsOptimized(TFChunk.TFChunk chunk){
            TFChunk.TFChunk value;
            return _cache.TryGet(chunk.FileName, out value);
        }
    }
}