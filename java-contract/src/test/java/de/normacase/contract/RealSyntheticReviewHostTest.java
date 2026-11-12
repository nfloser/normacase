package de.normacase.contract;

import static org.junit.jupiter.api.Assertions.*;
import java.util.Base64;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable;

/** Only enabled by CI against the isolated, synthetic .NET/PostgreSQL host. */
@EnabledIfEnvironmentVariable(named = "NORMACASE_JAVA_INTEGRATION", matches = "true")
class RealSyntheticReviewHostTest {
    @Test void forwardsIdentityToExistingServerAndRetainsCaseScope() throws Exception {
        String credential = System.getenv("NORMACASE_JAVA_TEST_CREDENTIAL");
        assertNotNull(credential);
        var client = new LoopbackReviewQueueClient(5080);
        var page = client.read(credential, null);
        var ids = page.queues().stream().flatMap(queue -> queue.items().stream())
            .map(ReviewQueuePage.Item::caseId).toList();
        assertEquals(java.util.List.of("demo-g-review"), ids);
        var denied = assertThrows(LoopbackReviewQueueClient.QueueReadException.class,
            () -> client.read(Base64.getEncoder().encodeToString(new byte[32]), null));
        assertEquals(LoopbackReviewQueueClient.Failure.UNAUTHORIZED, denied.failure());
    }
}
