using System;
using System.Collections.Generic;
using System.Security.Principal;
using EventStore.Core.Data;
using EventStore.Core.Messages;

namespace EventStore.Core.Helpers
{
    public static class IODispatcherAsync
    {
        public delegate void Step(IEnumerator<Step> nextSteps);

        public static void Run(this IEnumerable<Step> actions)
        {
            var actionsEnumerator = actions.GetEnumerator();
            Run(actionsEnumerator);
        }

        public static void Run(this Step action)
        {
            Run(new[] {action});
        }

        public static Step BeginReadForward(
            this IODispatcher ioDispatcher,
            string streamId,
            int fromEventNumber,
            int maxCount,
            bool resolveLinks,
            IPrincipal principal,
            Action<ClientMessage.ReadStreamEventsForwardCompleted> handler)
        {
            return
                steps =>
                    ioDispatcher.ReadForward(
                        streamId,
                        fromEventNumber,
                        maxCount,
                        resolveLinks,
                        principal,
                        response =>
                        {
                            handler(response);
                            Run(steps);
                        });
        }

        public static Step BeginReadBackward(
            this IODispatcher ioDispatcher,
            string streamId,
            int fromEventNumber,
            int maxCount,
            bool resolveLinks,
            IPrincipal principal,
            Action<ClientMessage.ReadStreamEventsBackwardCompleted> handler)
        {
            return
                steps =>
                    ioDispatcher.ReadBackward(
                        streamId,
                        fromEventNumber,
                        maxCount,
                        resolveLinks,
                        principal,
                        response =>
                        {
                            handler(response);
                            Run(steps);
                        });
        }

        public static Step BeginWriteEvents(
            this IODispatcher ioDispatcher,
            string streamId,
            int expectedVersion,
            IPrincipal principal,
            Event[] events,
            Action<ClientMessage.WriteEventsCompleted> handler)
        {
            return
                steps =>
                    WriteEventsWithRetry(ioDispatcher, streamId, expectedVersion, principal, events, handler, steps);
        }

        public static Step BeginDeleteStream(
            this IODispatcher ioDispatcher,
            string streamId,
            int expectedVersion,
            bool hardDelete,
            IPrincipal principal,
            Action<ClientMessage.DeleteStreamCompleted> handler)
        {
            return
                steps =>
                    DeleteStreamWithRetry(
                        ioDispatcher,
                        streamId,
                        expectedVersion,
                        hardDelete,
                        principal,
                        handler,
                        steps);
        }

        public static Step BeginSubscribeAwake(
            this IODispatcher ioDispatcher,
            string streamId,
            TFPos from,
            Action<IODispatcherDelayedMessage> handler,
            Guid? correlationId = null)
        {
            return steps => ioDispatcher.SubscribeAwake(
                streamId,
                @from,
                message =>
                {
                    handler(message);
                    Run(steps);
                },
                correlationId);
        }

        public static Step BeginDelay(this IODispatcher ioDispatcher, TimeSpan timeout, Action handler)
        {
            return steps => ioDispatcher.Delay(
                timeout,
                () =>
                {
                    handler();
                    Run(steps);
                });
        }

        private static void WriteEventsWithRetry(
            this IODispatcher ioDispatcher,
            string streamId,
            int expectedVersion,
            IPrincipal principal,
            Event[] events,
            Action<ClientMessage.WriteEventsCompleted> handler,
            IEnumerator<Step> steps)
        {
            PerformWithRetry(
                ioDispatcher,
                handler,
                steps,
                expectedVersion == ExpectedVersion.Any,
                TimeSpan.FromMilliseconds(100),
                action =>
                    ioDispatcher.WriteEvents(
                        streamId,
                        expectedVersion,
                        events,
                        principal,
                        response => action(response, response.Result)));
        }

        private static void DeleteStreamWithRetry(
            this IODispatcher ioDispatcher,
            string streamId,
            int expectedVersion,
            bool hardDelete,
            IPrincipal principal,
            Action<ClientMessage.DeleteStreamCompleted> handler,
            IEnumerator<Step> steps)
        {
            PerformWithRetry(
                ioDispatcher,
                handler,
                steps,
                expectedVersion == ExpectedVersion.Any,
                TimeSpan.FromMilliseconds(100),
                action =>
                    ioDispatcher.DeleteStream(
                        streamId,
                        expectedVersion,
                        hardDelete,
                        principal,
                        response => action(response, response.Result)));
        }

        private static void PerformWithRetry<T>(
            this IODispatcher ioDispatcher,
            Action<T> handler,
            IEnumerator<Step> steps,
            bool retryExpectedVersion,
            TimeSpan timeout,
            Action<Action<T, OperationResult>> action)
        {
            action(
                (response, result) =>
                {
                    if (ShouldRetry(result, retryExpectedVersion))
                    {
                        ioDispatcher.Delay(
                            timeout,
                            () =>
                            {
                                if (timeout < TimeSpan.FromSeconds(10))
                                    timeout += timeout;
                                PerformWithRetry(ioDispatcher, handler, steps, retryExpectedVersion, timeout, action);
                            });
                    }
                    else
                    {
                        handler(response);
                        Run(steps);
                    }
                });
        }

        private static bool ShouldRetry(OperationResult result, bool retryExpectedVersion)
        {
            switch (result)
            {
                case OperationResult.CommitTimeout:
                case OperationResult.ForwardTimeout:
                case OperationResult.PrepareTimeout:
                    return true;
                case OperationResult.WrongExpectedVersion:
                    return retryExpectedVersion;
                default:
                    return false;
            }
        }

        private static void Run(params Step[] actions)
        {
            var actionsEnumerator = ((IEnumerable<Step>) actions).GetEnumerator();
            Run(actionsEnumerator);
        }

        private static void Run(IEnumerator<Step> actions)
        {
            if (actions.MoveNext())
            {
                var action = actions.Current;
                action(actions);
            }
        }
    }
}
