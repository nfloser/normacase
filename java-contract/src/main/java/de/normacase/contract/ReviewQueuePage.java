package de.normacase.contract;

import java.util.List;

/** Wire data only: no interpretation of case, state, or queue identifiers. */
public record ReviewQueuePage(String configurationId, int configurationVersion,
                              String nextPageCursor, List<Queue> queues) {
    public ReviewQueuePage { queues = List.copyOf(queues); }
    public record Queue(String queueId, List<Item> items) {
        public Queue { items = List.copyOf(items); }
    }
    public record Item(String caseId, String caseRevision, String processRevision, String stateId,
                       String assessmentId, String auditRevision, boolean batchAllowed) { }
}
