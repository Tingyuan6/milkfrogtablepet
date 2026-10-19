package com.naifrog.pet;

import java.util.Map;
import java.util.Set;
import java.util.concurrent.CompletableFuture;

/** Reserved extension points; no model, memory or tool is enabled in V0.1. */
public final class IntelligenceContracts {
    private IntelligenceContracts() {}

    public interface ConversationProvider {
        CompletableFuture<String> reply(String input);

        void cancel();
    }

    public interface MemoryStore {
        CompletableFuture<Map<String, String>> read();

        CompletableFuture<Void> forget(String key);
    }

    public interface ToolRegistry {
        Set<String> names();
    }

    public interface PetIntentBridge {
        void requestIntent(String intent);
    }
}
