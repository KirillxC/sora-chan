import os
import time
from dotenv import load_dotenv
from groq import Groq

load_dotenv()

api_key = os.getenv("GROQ_API_KEY")
if not api_key:
    raise ValueError("GROQ_API_KEY не найден в .env")

# В Groq SDK эндпоинт по умолчанию /openai/v1, поэтому указываем его в base_url
client = Groq(
    api_key=api_key,
    base_url="https://api.groq.com/openai/v1",
)

start_time = time.perf_counter()
first_token_time = None
full_text = []

try:
    stream = client.chat.completions.create(
        model="llama-3.1-8b-instant",
        messages=[{"role": "user", "content": "Скажи одно слово: тест"}],
        stream=True,
    )

    for chunk in stream:
        delta = chunk.choices[0].delta.content or ""
        if delta:
            if first_token_time is None:
                first_token_time = time.perf_counter() - start_time
            full_text.append(delta)

    total_time = time.perf_counter() - start_time

    print(f"Ответ: {''.join(full_text)}")
    print(f"TTFT: {first_token_time:.3f} сек")
    print(f"Общее время: {total_time:.3f} сек")

except Exception as e:
    print(f"Ошибка запроса: {e}")