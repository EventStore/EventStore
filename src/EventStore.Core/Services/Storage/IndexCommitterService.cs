using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage
{
    public interface IIndexCommitterService
    {
        void Init(long checkpointPosition);
        void Stop();
        long GetCommitLastEventNumber(CommitLogRecord record);
        void AddPendingPrepare(PrepareLogRecord[] prepares, long postPosition);
        void AddPendingCommit(CommitLogRecord commit, long postPosition);
    }

    public class IndexCommitterService : IIndexCommitterService,
                                         IMonitoredQueue,
                                         IHandle<SystemMessage.StateChangeMessage>,
                                         IHandle<SystemMessage.BecomeShuttingDown>,
                                         IHandle<StorageMessage.CommitAck>
    {
        private readonly ILogger Log = LogManager.GetLoggerFor<IndexCommitterService>();
        private readonly IIndexCommitter _indexCommitter;
        private readonly IPublisher _publisher;
        private readonly ICheckpoint _replicationCheckpoint;
        private readonly ICheckpoint _writerCheckpoint;
        private readonly int _commitCount;
        private Thread _thread;
        private bool _stop;
        private VNodeState _state;

        public string Name { get { return _queueStats.Name; } }
        private readonly QueueStatsCollector _queueStats = new QueueStatsCollector("Index Committer");

        private readonly ConcurrentQueue<StorageMessage.CommitAck> _replicatedQueue = new ConcurrentQueue<StorageMessage.CommitAck>();
        private readonly ConcurrentDictionary<long, PendingTransaction> _pendingTransactions =
                            new ConcurrentDictionary<long, PendingTransaction>();

        private readonly CommitAckLinkedList _commitAcks = new CommitAckLinkedList();

        public IndexCommitterService(IIndexCommitter indexCommitter, IPublisher publisher, ICheckpoint replicationCheckpoint, ICheckpoint writerCheckpoint, int commitCount)
        {
            Ensure.NotNull(indexCommitter, "indexCommitter");
            Ensure.NotNull(publisher, "publisher");
            Ensure.NotNull(replicationCheckpoint, "replicationCheckpoint");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
            Ensure.Positive(commitCount, "commitCount");

            _indexCommitter = indexCommitter;
            _publisher = publisher;
            _replicationCheckpoint = replicationCheckpoint;
            _writerCheckpoint = writerCheckpoint;
            _commitCount = commitCount;
        }

        public void Init(long checkpointPosition)
        {
            _indexCommitter.Init(checkpointPosition);
            _thread = new Thread(HandleReplicatedQueue);
            _thread.IsBackground = true;
            _thread.Name = Name;
            _thread.Start();
        }

        public void Stop()
        {
            _stop = true;
        }

        public void HandleReplicatedQueue()
        {
            _queueStats.Start();
            QueueMonitor.Default.Register(this);

            try
            {
                StorageMessage.CommitAck replicatedMessage;
                while(!_stop)
                {
                    _queueStats.EnterBusy();
                    if (_replicatedQueue.TryDequeue(out replicatedMessage))
                    {
                        _queueStats.ProcessingStarted(replicatedMessage.GetType(), 0);
                        ProcessCommitReplicated(replicatedMessage);
                        _queueStats.ProcessingEnded(1);
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            }
            catch (Exception exc)
            {
                _queueStats.EnterIdle();
                _queueStats.ProcessingStarted<FaultedIndexCommitterServiceState>(0);
                Log.FatalException(exc, "Error in IndexCommitterService. Terminating...");
                Application.Exit(ExitCode.Error, "Error in IndexCommitterService. Terminating...\nError: " + exc.Message);
                while (!_stop)
                {
                    Thread.Sleep(100);
                }
               _queueStats.ProcessingEnded(0);
            }
            _publisher.Publish(new SystemMessage.ServiceShutdown(Name));

            _queueStats.EnterIdle();
            _queueStats.Stop();
            QueueMonitor.Default.Unregister(this);
        }

        private void ProcessCommitReplicated(StorageMessage.CommitAck message)
        {
            PendingTransaction transaction;
            long lastEventNumber = message.LastEventNumber;
            if(_pendingTransactions.TryRemove(message.TransactionPosition, out transaction))
            {
                var isTfEof = IsTfEof(transaction.PostPosition);
                if (transaction.Prepares.Count > 0)
                {
                    _indexCommitter.Commit(transaction.Prepares, isTfEof, true);
                }
                if(transaction.Commit != null)
                {
                    lastEventNumber = _indexCommitter.Commit(transaction.Commit, isTfEof, true);
                }
            }
            lastEventNumber = lastEventNumber == EventNumber.Invalid ? message.LastEventNumber : lastEventNumber;
            
            _replicationCheckpoint.Write(message.LogPosition);
            _publisher.Publish(new StorageMessage.CommitReplicated(message.CorrelationId, message.LogPosition, 
                message.TransactionPosition, message.FirstEventNumber, lastEventNumber));
        }

        private bool IsTfEof(long postPosition)
        {
            return postPosition == _writerCheckpoint.Read();
        }

        public long GetCommitLastEventNumber(CommitLogRecord commit)
        {
            return _indexCommitter.GetCommitLastEventNumber(commit);
        }

        public void AddPendingPrepare(PrepareLogRecord[] prepares, long postPosition)
        {
            var transactionPosition = prepares[0].TransactionPosition;
            PendingTransaction transaction;
            if (_pendingTransactions.TryGetValue(transactionPosition, out transaction))
            {
                var newTransaction = new PendingTransaction(transactionPosition, postPosition, transaction.Prepares, transaction.Commit);
                newTransaction.AddPendingPrepares(prepares);
                if(!_pendingTransactions.TryUpdate(transactionPosition, newTransaction, transaction))
                {
                    throw new InvalidOperationException("Failed to update pending prepare");
                }
            }
            else
            {
                var pendingTransaction = new PendingTransaction(transactionPosition, postPosition, prepares);
                if(!_pendingTransactions.TryAdd(transactionPosition, pendingTransaction))
                {
                    throw new InvalidOperationException("Failed to add pending prepare");
                }
            }
        }

        public void AddPendingCommit(CommitLogRecord commit, long postPosition)
        {
            PendingTransaction transaction;
            if (_pendingTransactions.TryGetValue(commit.TransactionPosition, out transaction))
            {
                var newTransaction = new PendingTransaction(commit.TransactionPosition, postPosition, transaction.Prepares, commit);
                if(!_pendingTransactions.TryUpdate(commit.TransactionPosition, newTransaction, transaction))
                {
                    throw new InvalidOperationException("Failed to update pending commit");
                }
            }
            else
            {
                var pendingTransaction = new PendingTransaction(commit.TransactionPosition, postPosition, commit);
                if(!_pendingTransactions.TryAdd(commit.TransactionPosition, pendingTransaction))
                {
                    throw new InvalidOperationException("Failed to add pending commit");
                }
            }
        }

        public void Handle(SystemMessage.StateChangeMessage msg)
        {
            if (_state == VNodeState.Master && msg.State != VNodeState.Master)
            {
                var commits = _commitAcks.GetAllCommitAcks();
                foreach (var commit in commits)
                {
                    CommitReplicated(commit.CommitAcks[0]);
                }
                _commitAcks.ClearCommitAcks();
            }
            _state = msg.State;
        }

        public void Handle(SystemMessage.BecomeShuttingDown message)
        {
            _stop = true;
        }

        public void Handle(StorageMessage.CommitAck message)
        {
            if(_state != VNodeState.Master || _commitCount == 1)
            {
                _replicatedQueue.Enqueue(message);
                return;
            }

            var checkpoint = _replicationCheckpoint.ReadNonFlushed();
            if(message.LogPosition <= checkpoint) return;

            var res = _commitAcks.AddCommitAck(message);
            if (res.IsReplicated(_commitCount))
            {
                EnqueueCommitsUpToPosition(message);
            }
        }

        private void EnqueueCommitsUpToPosition(StorageMessage.CommitAck message)
        {
            var commits = _commitAcks.GetCommitAcksUpTo(message);
            foreach (var commit in commits)
            {
                CommitReplicated(commit.CommitAcks[0]);
            }
            _commitAcks.RemoveCommitAcks(commits);
        }

        private void CommitReplicated(StorageMessage.CommitAck message)
        {
            _replicatedQueue.Enqueue(message);
        }

        public QueueStats GetStatistics()
        {
            return _queueStats.GetStatistics(0);
        }

        private class FaultedIndexCommitterServiceState
        {
        }

        internal class PendingTransaction
        {
            public readonly List<PrepareLogRecord> Prepares = new List<PrepareLogRecord>();
            private CommitLogRecord _commit;
            public CommitLogRecord Commit { get { return _commit; } }
            public readonly long TransactionPosition;
            public readonly long PostPosition;

            public PendingTransaction(long transactionPosition, long postPosition, IEnumerable<PrepareLogRecord> prepares, CommitLogRecord commit = null)
            {
                TransactionPosition = transactionPosition;
                PostPosition = postPosition;
                Prepares.AddRange(prepares);
                _commit = commit;
            }

            public PendingTransaction(long transactionPosition, long postPosition, CommitLogRecord commit)
            {
                TransactionPosition = transactionPosition;
                PostPosition = postPosition;
                _commit = commit;
            }

            public void AddPendingPrepares(IEnumerable<PrepareLogRecord> prepares)
            {
                Prepares.AddRange(prepares);
            }

            public void SetPendingCommit(CommitLogRecord commit)
            {
                _commit = commit;
            }
        }

        internal class CommitAckLinkedList
        {
            private readonly Dictionary<Guid, LinkedListNode<CommitAckNode>> _commitAckNodes =
                new Dictionary<Guid, LinkedListNode<CommitAckNode>>();
            private readonly LinkedList<CommitAckNode> _commitAcksLinkedList =
                new LinkedList<CommitAckNode>();

            public CommitAckNode AddCommitAck(StorageMessage.CommitAck message)
            {
                LinkedListNode<CommitAckNode> commitAckNode;

                if (_commitAckNodes.TryGetValue(message.CorrelationId, out commitAckNode))
                {
                    commitAckNode.Value.AddCommitAck(message);
                }
                else
                {
                    var newCommitAck = new CommitAckNode(message.CorrelationId, message);
                    commitAckNode = _commitAcksLinkedList.AddLast(newCommitAck);
                    _commitAckNodes.Add(message.CorrelationId, commitAckNode);
                }

                // ensure commit acks are sorted
                var currentNode = commitAckNode;
                var previousNode = commitAckNode.Previous;

                while (previousNode != null && previousNode.Value.LogPosition > currentNode.Value.LogPosition)
                {
                    _commitAcksLinkedList.Remove(previousNode);
                    _commitAcksLinkedList.AddAfter(currentNode, previousNode);
                    previousNode = currentNode.Previous;
                }
                return commitAckNode.Value;
            }

            public List<CommitAckNode> GetAllCommitAcks()
            {
                var currentNode = _commitAcksLinkedList.First;
                var result = new List<CommitAckNode>();

                while (currentNode != null)
                {
                    result.Add(currentNode.Value);
                    currentNode = currentNode.Next;
                }
                return result;
            }

            public List<CommitAckNode> GetCommitAcksUpTo(StorageMessage.CommitAck message)
            {
                LinkedListNode<CommitAckNode> commitAckNode;

                if (_commitAckNodes.TryGetValue(message.CorrelationId, out commitAckNode))
                {
                    var currentNode = commitAckNode;
                    // Ensure that we have all nodes at this position
                    while (currentNode.Next != null && currentNode.Next.Value.LogPosition == currentNode.Value.LogPosition)
                    {
                        currentNode = currentNode.Next;
                    }

                    var result = new List<CommitAckNode>();
                    do
                    {
                        result.Add(currentNode.Value);
                        currentNode = currentNode.Previous;
                    } while (currentNode != null);

                    result.Reverse();
                    return result;
                }
                else
                {
                    throw new InvalidOperationException("Commit ack not present in node list.");
                }
            }

            public void ClearCommitAcks()
            {
                _commitAckNodes.Clear();
                _commitAcksLinkedList.Clear();
            }

            public void RemoveCommitAcks(List<CommitAckNode> commitAcks)
            {
                foreach (var commitAck in commitAcks)
                {
                    LinkedListNode<CommitAckNode> commitAckNode;
                    if(_commitAckNodes.TryGetValue(commitAck.CorrelationId, out commitAckNode))
                    {
                        _commitAcksLinkedList.Remove(commitAckNode);
                        _commitAckNodes.Remove(commitAck.CorrelationId);
                    }
                    else
                    {
                        throw new InvalidOperationException("Commit ack not present in node list");
                    }
                }
            }

            internal class CommitAckNode
            {
                public readonly Guid CorrelationId;
                public readonly long LogPosition;
                public readonly List<StorageMessage.CommitAck> CommitAcks = new List<StorageMessage.CommitAck>();
                private bool _hadSelf;

                public CommitAckNode(Guid correlationId, StorageMessage.CommitAck commitAck)
                {
                    CorrelationId = correlationId;
                    LogPosition = commitAck.LogPosition;
                    AddCommitAck(commitAck);
                }

                public void AddCommitAck(StorageMessage.CommitAck commitAck)
                {
                    Ensure.Equal(true, CorrelationId == commitAck.CorrelationId, "correlationId should be equal");

                    CommitAcks.Add(commitAck);
                    if (commitAck.IsSelf)
                        _hadSelf = true;
                }

                public bool IsReplicated(int commitCount)
                {
                    return CommitAcks.Count >= commitCount && _hadSelf;
                }
            }
        }
    }
}













