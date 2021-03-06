using System;


namespace Orleans.Runtime.Messaging
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
    internal class InboundMessageQueue : IInboundMessageQueue
    {
        private readonly RuntimeQueue<Message>[] messageQueues;
        private readonly TraceLogger log;
        private readonly QueueTrackingStatistic[] queueTracking;

        public int Count
        {
            get
            {
                int n = 0;
                foreach (var queue in messageQueues)
                    n += queue.Count;
                
                return n;
            }
        }

        internal InboundMessageQueue()
        {
            int n = Enum.GetValues(typeof(Message.Categories)).Length;
            messageQueues = new RuntimeQueue<Message>[n];
            queueTracking = new QueueTrackingStatistic[n];
            int i = 0;
            foreach (var category in Enum.GetValues(typeof(Message.Categories)))
            {
                messageQueues[i] = new RuntimeQueue<Message>();
                if (StatisticsCollector.CollectQueueStats)
                {
                    var queueName = "IncomingMessageAgent." + category;
                    queueTracking[i] = new QueueTrackingStatistic(queueName);
                    queueTracking[i].OnStartExecution();
                }
                i++;
            }
            log = TraceLogger.GetLogger("Orleans.Messaging.InboundMessageQueue");
        }

        public void Stop()
        {
            if (messageQueues == null) return;
            foreach (var q in messageQueues)
                q.CompleteAdding();
            
            if (!StatisticsCollector.CollectQueueStats) return;

            foreach (var q in queueTracking)
                q.OnStopExecution();
        }

        public void PostMessage(Message msg)
        {
            if (Message.WriteMessagingTraces) msg.AddTimestamp(Message.LifecycleTag.EnqueueIncoming);

#if TRACK_DETAILED_STATS
            if (StatisticsCollector.CollectQueueStats)
            {
                queueTracking[(int)msg.Category].OnEnQueueRequest(1, messageQueues[(int)msg.Category].Count, msg);
            }
#endif
            messageQueues[(int)msg.Category].Add(msg);
           
            if (log.IsVerbose3) log.Verbose3("Queued incoming {0} message", msg.Category.ToString());
        }

        public Message WaitMessage(Message.Categories type)
        {
            try
            {
                Message msg = messageQueues[(int)type].Take();

#if TRACK_DETAILED_STATS
                if (StatisticsCollector.CollectQueueStats)
                {
                    queueTracking[(int)msg.Category].OnDeQueueRequest(msg);
                }
#endif
                return msg;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
