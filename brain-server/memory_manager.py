import os
import json
import time
from typing import List, Dict, Any, Optional

try:
    import chromadb
    from chromadb.config import Settings
    HAS_CHROMA = True
except ImportError:
    HAS_CHROMA = False

class MemoryManager:
    """
    3-tier Hierarchical Memory:
    1. Working Context: recent ~15 events and dialog lines.
    2. Episodic Rolling Buffer: compressed summaries of past interactions.
    3. Long-term Vector DB (ChromaDB): semantic search for facts, habits, past days.
    """
    def __init__(self, db_path: str = "/home/kiriewka/all/ai-chan/brain-server/chroma_db"):
        self.db_path = db_path
        self.working_context: List[Dict[str, Any]] = []
        self.max_working_events = 20
        self.session_summary: str = "Начало новой сессии. Мы находимся в интерактивной комнате."

        self.collection = None
        if HAS_CHROMA:
            try:
                os.makedirs(db_path, exist_ok=True)
                self.client = chromadb.PersistentClient(path=db_path)
                self.collection = self.client.get_or_create_collection(name="ai_chan_episodic_memory")
            except Exception as e:
                print(f"[MemoryManager] ChromaDB init error: {e}")

    def add_event(self, role: str, content: str, metadata: Optional[Dict[str, Any]] = None):
        event = {
            "role": role,  # "user", "ai_speech", "ai_thought", "touch_sense", "body_language"
            "content": content,
            "metadata": metadata or {},
            "timestamp": time.time()
        }
        self.working_context.append(event)
        if len(self.working_context) > self.max_working_events:
            self._compress_oldest_events()

    def query_memories(self, query_text: str, n_results: int = 3) -> List[str]:
        if not self.collection:
            return []
        try:
            results = self.collection.query(query_texts=[query_text], n_results=n_results)
            documents = results.get("documents", [[]])[0]
            return documents
        except Exception as e:
            print(f"[MemoryManager] Query error: {e}")
            return []

    def store_fact(self, fact_text: str, category: str = "fact"):
        if not self.collection:
            return
        try:
            doc_id = f"fact_{int(time.time() * 1000)}"
            self.collection.add(
                documents=[fact_text],
                metadatas=[{"category": category, "timestamp": time.time()}],
                ids=[doc_id]
            )
        except Exception as e:
            print(f"[MemoryManager] Store error: {e}")

    def _compress_oldest_events(self):
        # Take the oldest 5 events and summarize them
        events_to_compress = self.working_context[:5]
        self.working_context = self.working_context[5:]
        summary_chunk = "; ".join([f"{e['role']}: {e['content']}" for e in events_to_compress])
        # Save compressed chunk into long term memory
        self.store_fact(f"События ранее: {summary_chunk}", category="episodic_summary")

    def get_prompt_context(self, current_query: str = "") -> str:
        relevant_memories = self.query_memories(current_query) if current_query else []
        memories_str = "\n".join([f"- {m}" for m in relevant_memories]) if relevant_memories else "Нет специфичных прошлых воспоминаний."

        recent_dialog = "\n".join([
            f"[{e['role'].upper()}]: {e['content']}" for e in self.working_context[-10:]
        ])

        return f"""
[КРАТКОЕ РЕЗЮМЕ СЕССИИ]
{self.session_summary}

[ИЗВЛЕЧЕННЫЕ ДОЛГОСРОЧНЫЕ ВОСПОМИНАНИЯ]
{memories_str}

[ПОСЛЕДНИЕ СОБЫТИЯ И РЕПЛИКИ]
{recent_dialog}
"""
