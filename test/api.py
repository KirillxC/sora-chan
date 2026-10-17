import os
import csv
import time
import json
import re
from datetime import datetime
from pathlib import Path
import httpx

from textual.app import App, ComposeResult
from textual.containers import Horizontal, Vertical, VerticalScroll
from textual.screen import ModalScreen
from textual.widgets import (
    Header, Footer, Select, Button, Static, 
    Collapsible, DataTable, Label, Input
)
from textual import work
from textual.binding import Binding

ENV_PATH = Path(__file__).resolve().parent / ".env"
CONFIG_PATH = Path(__file__).resolve().parent / "providers.json"
CSV_PATH = Path(__file__).resolve().parent / "benchmark_results.csv"

PROMPT = (
    "Напиши подробную техническую статью на 600 слов про устройство "
    "сетевого стека в ядре Linux (sk_buff, NAPI, eBPF). Без воды."
)


def load_env() -> dict[str, str]:
    env_vars: dict[str, str] = {}
    if ENV_PATH.exists():
        for line in ENV_PATH.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, val = line.split("=", 1)
            key = key.removeprefix("export ").strip()
            val = val.strip().strip("'\"")
            env_vars[key] = val
            os.environ.setdefault(key, val)
    return env_vars


def save_env_key(key: str, val: str) -> None:
    lines: list[str] = []
    if ENV_PATH.exists():
        lines = ENV_PATH.read_text(encoding="utf-8").splitlines()
    
    updated = False
    new_lines = []
    for line in lines:
        if line.startswith(f"{key}=") or line.startswith(f"export {key}="):
            new_lines.append(f"{key}={val}")
            updated = True
        else:
            new_lines.append(line)
    if not updated:
        new_lines.append(f"{key}={val}")
    
    ENV_PATH.write_text("\n".join(new_lines) + "\n", encoding="utf-8")
    os.environ[key] = val


def get_default_config() -> dict:
    return {
        "active_provider": "",
        "providers": []
    }


def load_config() -> dict:
    if CONFIG_PATH.exists():
        try:
            return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
        except Exception:
            pass
    cfg = get_default_config()
    save_config(cfg)
    return cfg


def save_config(cfg: dict) -> None:
    CONFIG_PATH.write_text(json.dumps(cfg, indent=2, ensure_ascii=False), encoding="utf-8")


def resolve_api_key(prov: dict, env_vars: dict[str, str]) -> str:
    if prov.get("api_key"):
        return prov["api_key"]
    env_name = prov.get("api_key_env")
    if env_name:
        if env_name in env_vars:
            return env_vars[env_name]
        if os.getenv(env_name):
            return os.getenv(env_name) or ""
    
    ptype = prov.get("type", "openai")
    if ptype == "gemini":
        for k in ("GEMINI_API_KEY", "GOOGLE_API_KEY", "API_KEY"):
            val = env_vars.get(k) or os.getenv(k)
            if val:
                return val
    elif ptype == "openai":
        for k in (f"{prov.get('id', '').upper()}_API_KEY", "GROQ_API_KEY", "OPENAI_API_KEY", "API_KEY"):
            val = env_vars.get(k) or os.getenv(k)
            if val:
                return val
    return ""


class AddProviderModal(ModalScreen[dict | None]):
    """Модальное окно добавления OpenAI-совместимого или Gemini провайдера"""

    DEFAULT_CSS = """
    AddProviderModal {
        align: center middle;
        background: rgba(0, 0, 0, 0.7);
    }
    #modal-box {
        width: 74;
        height: auto;
        padding: 1 2;
        background: $surface;
        border: thick $primary;
    }
    #modal-title {
        text-align: center;
        text-style: bold;
        color: $accent;
        margin-bottom: 1;
    }
    .form-label {
        margin-top: 1;
        color: $text;
        text-style: bold;
    }
    .form-desc {
        color: $text-muted;
        margin-bottom: 0;
    }
    .input-field {
        margin-bottom: 1;
    }
    #modal-error {
        color: $error;
        text-style: bold;
        margin-top: 1;
        min-height: 1;
    }
    #modal-buttons {
        margin-top: 1;
        height: 3;
        align: right middle;
    }
    #modal-buttons Button {
        margin-left: 2;
        min-width: 14;
    }
    """

    BINDINGS = [
        Binding("escape", "cancel", "Отмена"),
    ]

    def compose(self) -> ComposeResult:
        with Vertical(id="modal-box"):
            yield Label("➕ Добавить нового API провайдера", id="modal-title")
            
            yield Label("Название провайдера:", classes="form-label")
            yield Input(placeholder="Например: Groq, OpenRouter, Ollama, DeepSeek", id="inp-name", classes="input-field")

            yield Label("Тип API:", classes="form-label")
            yield Select(
                [
                    ("OpenAI-совместимый (/v1/chat/completions)", "openai"),
                    ("Google Gemini API (/v1beta/models)", "gemini")
                ],
                value="openai",
                id="sel-type",
                classes="input-field"
            )

            yield Label("Эндпоинт (Base URL):", classes="form-label")
            yield Label("Для OpenAI: https://api.groq.com/openai/v1 или http://localhost:11434/v1", classes="form-desc")
            yield Input(placeholder="https://api.groq.com/openai/v1", id="inp-url", classes="input-field")

            yield Label("API Ключ или переменная окружения:", classes="form-label")
            yield Label("Укажи имя переменной (GROQ_API_KEY) или сам ключ", classes="form-desc")
            yield Input(placeholder="GROQ_API_KEY или gsk_...", id="inp-key", classes="input-field")

            yield Label("", id="modal-error")

            with Horizontal(id="modal-buttons"):
                yield Button("Отмена", variant="default", id="btn-cancel")
                yield Button("Сохранить", variant="success", id="btn-save")

    def on_button_pressed(self, event: Button.Pressed) -> None:
        if event.button.id == "btn-cancel":
            self.action_cancel()
        elif event.button.id == "btn-save":
            self.submit()

    def action_cancel(self) -> None:
        self.dismiss(None)

    def submit(self) -> None:
        name = self.query_one("#inp-name", Input).value.strip()
        ptype = self.query_one("#sel-type", Select).value
        url = self.query_one("#inp-url", Input).value.strip()
        key_or_env = self.query_one("#inp-key", Input).value.strip()
        err = self.query_one("#modal-error", Label)

        if not name:
            err.update("[!] Укажи название провайдера!")
            return
        if not url:
            err.update("[!] Укажи базовый эндпоинт (URL)!")
            return

        pid = re.sub(r"[^a-zA-Z0-9_\-]+", "_", name.lower()).strip("_")
        if not pid:
            pid = "custom_provider"

        api_key_env = ""
        api_key = ""
        if key_or_env:
            if re.match(r"^[A-Z0-9_]+$", key_or_env):
                api_key_env = key_or_env
            else:
                api_key = key_or_env
                env_var_name = f"{pid.upper()}_API_KEY"
                save_env_key(env_var_name, api_key)
                api_key_env = env_var_name

        result = {
            "id": pid,
            "name": name,
            "type": ptype,
            "endpoints": [url],
            "api_key_env": api_key_env,
            "api_key": api_key
        }
        self.dismiss(result)


class LLMBenchTUI(App):
    CSS = """
    Screen {
        padding: 1 2;
    }
    #top-bar {
        height: 3;
        margin-bottom: 1;
    }
    #provider-select {
        width: 26;
        margin-right: 1;
    }
    #add-prov-btn {
        width: 5;
        min-width: 5;
        margin-right: 2;
    }
    #model-select {
        width: 1fr;
        margin-right: 2;
    }
    #run-btn {
        width: 22;
        min-width: 22;
    }
    #status-line {
        height: 1;
        margin-bottom: 1;
        color: $accent;
        text-style: bold;
    }
    DataTable {
        height: 11;
        margin-bottom: 1;
        border: tall $primary;
    }
    Collapsible {
        border: round $secondary;
        height: 1fr;
    }
    #spoiler-scroll {
        height: 1fr;
        padding: 0 1;
    }
    """

    BINDINGS = [
        Binding("q", "quit", "Выход"),
        Binding("ctrl+right", "next_provider", "След. провайдер (Ctrl+→)"),
        Binding("ctrl+left", "prev_provider", "Пред. провайдер (Ctrl+←)"),
        Binding("a", "add_provider", "➕ Добавить провайдер"),
        Binding("r", "run_bench", "Запустить тест"),
        Binding("e", "export_csv", "Экспорт CSV"),
        Binding("s", "toggle_spoiler", "Спойлер текста"),
    ]

    def __init__(self):
        super().__init__()
        self.env_vars = load_env()
        self.config = load_config()
        self.active_provider_idx = 0
        self.active_endpoint = ""
        self.results_history = []
        self._sync_active_provider_idx()

    def _sync_active_provider_idx(self) -> None:
        target_id = self.config.get("active_provider", "")
        providers = self.config.get("providers", [])
        for idx, p in enumerate(providers):
            if p.get("id") == target_id:
                self.active_provider_idx = idx
                return
        self.active_provider_idx = 0

    @property
    def current_provider(self) -> dict:
        providers = self.config.get("providers", [])
        if not providers:
            return {
                "id": "default",
                "name": "None",
                "type": "openai",
                "endpoints": [],
                "api_key_env": "",
                "api_key": ""
            }
        return providers[self.active_provider_idx % len(providers)]

    def compose(self) -> ComposeResult:
        yield Header(show_clock=True)
        with Vertical():
            with Horizontal(id="top-bar"):
                yield Select([], prompt="Провайдер...", id="provider-select")
                yield Button("➕", id="add-prov-btn", tooltip="Добавить провайдер (хоткей 'a')")
                yield Select([], prompt="Загрузка списка моделей...", id="model-select")
                yield Button("🚀 Замерить TPS", id="run-btn", variant="success", disabled=True)
            yield Label("[*] Инициализация...", id="status-line")
            yield DataTable(id="results-table")
            with Collapsible(title="Сгенерированный текст (нажми сюда или клавишу 's', чтобы развернуть)", collapsed=True, id="spoiler"):
                with VerticalScroll(id="spoiler-scroll"):
                    yield Static("Здесь появится ответ модели после запуска...", id="output-text")
        yield Footer()

    async def on_mount(self) -> None:
        table = self.query_one("#results-table", DataTable)
        table.add_columns(
            "Провайдер",
            "Модель",
            "TTFT (1-й токен)",
            "Время стрима",
            "Токены (Мысли)",
            "Чистый TPS 🚀",
            "Эффект. TPS ⚡"
        )
        table.cursor_type = "row"

        self._refresh_provider_ui()

    def _refresh_provider_ui(self) -> None:
        providers = self.config.get("providers", [])
        prov_select = self.query_one("#provider-select", Select)
        
        if not providers:
            self.title = "TPS Benchmark"
            self.sub_title = "Провайдеры не настроены | Нажми 'a' чтобы добавить"
            prov_select.set_options([])
            self.query_one("#status-line", Label).update(
                "[!] providers.json пуст. Нажми 'a' или '➕', чтобы добавить первый эндпоинт."
            )
            return

        prov_options = [(p.get("name", p["id"]), p["id"]) for p in providers]
        prov_select.set_options(prov_options)

        cur = self.current_provider
        prov_select.value = cur.get("id")

        self.title = f"TPS Benchmark — [{cur.get('name', 'API')}]"
        self.sub_title = f"{cur.get('type', '').upper()} | Ctrl+→ след. | 'a' добавить"

        self.fetch_models()

    def action_next_provider(self) -> None:
        providers = self.config.get("providers", [])
        if not providers:
            return
        self.active_provider_idx = (self.active_provider_idx + 1) % len(providers)
        self.config["active_provider"] = self.current_provider["id"]
        save_config(self.config)
        self._refresh_provider_ui()

    def action_prev_provider(self) -> None:
        providers = self.config.get("providers", [])
        if not providers:
            return
        self.active_provider_idx = (self.active_provider_idx - 1 + len(providers)) % len(providers)
        self.config["active_provider"] = self.current_provider["id"]
        save_config(self.config)
        self._refresh_provider_ui()

    def action_add_provider(self) -> None:
        def on_add(new_prov: dict | None) -> None:
            if not new_prov:
                return
            providers = self.config.setdefault("providers", [])
            existing_idx = next((i for i, p in enumerate(providers) if p["id"] == new_prov["id"]), None)
            if existing_idx is not None:
                providers[existing_idx] = new_prov
                self.active_provider_idx = existing_idx
            else:
                providers.append(new_prov)
                self.active_provider_idx = len(providers) - 1
            
            self.config["active_provider"] = new_prov["id"]
            save_config(self.config)
            self.env_vars = load_env()
            self._refresh_provider_ui()

        self.push_screen(AddProviderModal(), callback=on_add)

    def on_select_changed(self, event: Select.Changed) -> None:
        if event.select.id == "provider-select" and event.value != Select.BLANK:
            providers = self.config.get("providers", [])
            for idx, p in enumerate(providers):
                if p["id"] == event.value:
                    if idx != self.active_provider_idx:
                        self.active_provider_idx = idx
                        self.config["active_provider"] = p["id"]
                        save_config(self.config)
                        self._refresh_provider_ui()
                    break

    def on_button_pressed(self, event: Button.Pressed) -> None:
        if event.button.id == "run-btn":
            self.action_run_bench()
        elif event.button.id == "add-prov-btn":
            self.action_add_provider()

    def action_toggle_spoiler(self) -> None:
        spoiler = self.query_one("#spoiler", Collapsible)
        spoiler.collapsed = not spoiler.collapsed

    def action_export_csv(self) -> None:
        status = self.query_one("#status-line", Label)
        if not self.results_history:
            status.update("[!] Таблица пуста — сначала запусти хотя бы один замер!")
            return

        fieldnames = [
            "timestamp", "provider", "endpoint", "model", 
            "ttft_sec", "stream_duration_sec", "total_duration_sec",
            "output_tokens", "thought_tokens", "pure_tps", "effective_tps"
        ]

        try:
            with CSV_PATH.open("w", newline="", encoding="utf-8") as f:
                writer = csv.DictWriter(f, fieldnames=fieldnames)
                writer.writeheader()
                writer.writerows(self.results_history)
            status.update(f"[💾] Сохранено {len(self.results_history)} замеров в {CSV_PATH.name} (хоткей 'e')")
        except Exception as e:
            status.update(f"[!] Ошибка сохранения CSV: {e}")

    def action_run_bench(self) -> None:
        select = self.query_one("#model-select", Select)
        if select.value in (Select.BLANK, None):
            return
        self.stream_benchmark(str(select.value))

    @work(exclusive=True)
    async def fetch_models(self) -> None:
        status = self.query_one("#status-line", Label)
        model_select = self.query_one("#model-select", Select)
        btn = self.query_one("#run-btn", Button)

        btn.disabled = True
        model_select.set_options([])
        
        prov = self.current_provider
        self.env_vars = load_env()
        api_key = resolve_api_key(prov, self.env_vars)

        endpoints = prov.get("endpoints", [])
        if not endpoints:
            status.update(f"[!] У провайдера {prov.get('name')} не настроены эндпоинты в providers.json")
            return

        ptype = prov.get("type", "openai")

        async with httpx.AsyncClient(timeout=10.0, verify=False) as client:
            for ep in endpoints:
                clean_ep = ep.rstrip("/")
                status.update(f"[*] Запрашиваем модели {prov.get('name')} с {clean_ep} ...")
                try:
                    if ptype == "gemini":
                        headers = {"x-goog-api-key": api_key} if api_key else {}
                        resp = await client.get(f"{clean_ep}/v1beta/models", headers=headers)
                        resp.raise_for_status()
                        data = resp.json()
                        self.active_endpoint = clean_ep

                        models = []
                        for m in data.get("models", []):
                            methods = m.get("supportedGenerationMethods", [])
                            if "generateContent" in methods:
                                short_name = m["name"].replace("models/", "")
                                disp = m.get("displayName", short_name)
                                models.append((f"{short_name}  ({disp})", short_name))

                        models.sort(key=lambda x: x[1], reverse=True)
                    else:
                        # OpenAI-совместимый API (Groq, OpenRouter, Ollama, etc.)
                        headers = {}
                        if api_key:
                            headers["Authorization"] = f"Bearer {api_key}"
                        
                        resp = await client.get(f"{clean_ep}/models", headers=headers)
                        resp.raise_for_status()
                        data = resp.json()
                        self.active_endpoint = clean_ep

                        raw_list = data.get("data", []) if isinstance(data, dict) else data
                        models = []
                        for m in raw_list:
                            mid = m.get("id") if isinstance(m, dict) else str(m)
                            # Исключаем явные аудио/эмбеддинг модели
                            if any(x in mid.lower() for x in ("whisper", "tts", "embed", "moderation", "guard")):
                                continue
                            models.append((mid, mid))

                        models.sort(key=lambda x: x[1])

                    if models:
                        model_select.set_options(models)
                        # Выбираем дефолтную модель
                        default_m = None
                        if ptype == "gemini":
                            default_m = next((m[1] for m in models if "flash" in m[1]), models[0][1])
                        else:
                            # Для Groq / OpenAI предпочтем llama/qwen/deepseek или первую
                            preferred = ["llama-3.3-70b-versatile", "llama-3.1-8b-instant", "qwen", "deepseek"]
                            for pref in preferred:
                                found = next((m[1] for m in models if pref in m[1].lower()), None)
                                if found:
                                    default_m = found
                                    break
                            if not default_m:
                                default_m = models[0][1]

                        model_select.value = default_m
                        btn.disabled = False
                        status.update(
                            f"[+] {prov.get('name')} ({self.active_endpoint}) | Доступно моделей: {len(models)}"
                        )
                        return
                    else:
                        status.update(f"[-] Список моделей пуст на {clean_ep}.")
                        return

                except httpx.HTTPStatusError as e:
                    code = e.response.status_code
                    if code == 401 or code == 403:
                        status.update(f"[!] Ошибка {code} на {clean_ep}: Проверь API-ключ в .env ({prov.get('api_key_env')}) или прокси.")
                    else:
                        status.update(f"[-] HTTP {code} на {clean_ep}: {e}")
                except Exception as e:
                    status.update(f"[-] Ошибка подключения к {clean_ep}: {e}")

        status.update(f"[!] Не удалось подключиться к эндпоинтам {prov.get('name')}. Проверь сеть/ключ.")

    @work(exclusive=True)
    async def stream_benchmark(self, model: str) -> None:
        status = self.query_one("#status-line", Label)
        out_widget = self.query_one("#output-text", Static)
        spoiler = self.query_one("#spoiler", Collapsible)
        run_btn = self.query_one("#run-btn", Button)
        table = self.query_one("#results-table", DataTable)

        prov = self.current_provider
        self.env_vars = load_env()
        api_key = resolve_api_key(prov, self.env_vars)
        ptype = prov.get("type", "openai")

        run_btn.disabled = True
        out_widget.update("")
        spoiler.title = f"Ожидание первого токена от {model}..."
        status.update(f"[*] Отправлен запрос к {model} ({prov.get('name')})... Ждём TTFT...")

        start_time = time.perf_counter()
        first_chunk_time = None
        first_text_time = None
        accumulated_text = []
        char_count = 0
        out_tokens = 0
        thought_tokens = 0

        try:
            async with httpx.AsyncClient(timeout=httpx.Timeout(10.0, read=120.0), verify=False) as client:
                if ptype == "gemini":
                    url = f"{self.active_endpoint}/v1beta/models/{model}:streamGenerateContent?alt=sse"
                    headers = {
                        "Content-Type": "application/json",
                        "x-goog-api-key": api_key
                    }
                    payload = {
                        "contents": [{"parts": [{"text": PROMPT}]}],
                        "generationConfig": {"temperature": 0.7}
                    }

                    async with client.stream("POST", url, headers=headers, json=payload) as resp:
                        resp.raise_for_status()
                        async for line in resp.aiter_lines():
                            line = line.strip()
                            if not line or not line.startswith("data: "):
                                continue
                            data_str = line[6:].strip()
                            if data_str == "[DONE]":
                                break
                            try:
                                chunk = json.loads(data_str)
                            except json.JSONDecodeError:
                                continue

                            now = time.perf_counter()
                            if first_chunk_time is None:
                                first_chunk_time = now

                            candidates = chunk.get("candidates", [])
                            if candidates:
                                parts = candidates[0].get("content", {}).get("parts", [])
                                for part in parts:
                                    if part.get("thought"):
                                        status.update(f"[*] Модель размышляет... ({now - start_time:.1f} сек)")
                                        continue
                                    text = part.get("text", "")
                                    if text:
                                        if first_text_time is None:
                                            first_text_time = now
                                        accumulated_text.append(text)
                                        char_count += len(text)
                                        out_widget.update("".join(accumulated_text))
                                        ttft_live = first_text_time - start_time
                                        status.update(
                                            f"[*] Стриминг ({prov.get('name')})! TTFT: {ttft_live:.2f} с | Принято: {char_count} симв."
                                        )
                                        spoiler.title = f"Текст ответа ({char_count} симв., TTFT: {ttft_live:.2f} с)"

                            if "usageMetadata" in chunk:
                                usage = chunk["usageMetadata"]
                                out_tokens = usage.get("candidatesTokenCount", out_tokens)
                                thought_tokens = usage.get("thoughtsTokenCount", thought_tokens)

                else:
                    # OpenAI-совместимый стриминг (Groq, etc.)
                    url = f"{self.active_endpoint}/chat/completions"
                    headers = {
                        "Content-Type": "application/json",
                        "Authorization": f"Bearer {api_key}"
                    }
                    payload = {
                        "model": model,
                        "messages": [{"role": "user", "content": PROMPT}],
                        "stream": True,
                        "stream_options": {"include_usage": True}
                    }

                    async with client.stream("POST", url, headers=headers, json=payload) as resp:
                        resp.raise_for_status()
                        async for line in resp.aiter_lines():
                            line = line.strip()
                            if not line or not line.startswith("data: "):
                                continue
                            data_str = line[6:].strip()
                            if data_str == "[DONE]":
                                break
                            try:
                                chunk = json.loads(data_str)
                            except json.JSONDecodeError:
                                continue

                            now = time.perf_counter()
                            if first_chunk_time is None:
                                first_chunk_time = now

                            choices = chunk.get("choices", [])
                            if choices:
                                delta = choices[0].get("delta", {})
                                reasoning = delta.get("reasoning_content") or delta.get("reasoning")
                                if reasoning:
                                    status.update(f"[*] Размышления модели... ({now - start_time:.1f} с)")

                                content = delta.get("content", "")
                                if content:
                                    if first_text_time is None:
                                        first_text_time = now
                                    accumulated_text.append(content)
                                    char_count += len(content)
                                    out_widget.update("".join(accumulated_text))
                                    ttft_live = first_text_time - start_time
                                    status.update(
                                        f"[*] Стриминг ({prov.get('name')})! TTFT: {ttft_live:.2f} с | Принято: {char_count} симв."
                                    )
                                    spoiler.title = f"Текст ответа ({char_count} симв., TTFT: {ttft_live:.2f} с)"

                            usage = chunk.get("usage") or chunk.get("x_groq", {}).get("usage")
                            if usage:
                                out_tokens = usage.get("completion_tokens", out_tokens)
                                dtls = usage.get("completion_tokens_details", {})
                                if isinstance(dtls, dict):
                                    thought_tokens = dtls.get("reasoning_tokens", thought_tokens)

            end_time = time.perf_counter()

            ttft_point = first_text_time or first_chunk_time
            if ttft_point is None:
                status.update(f"[!] Модель {model} не вернула текста ответа.")
                return

            ttft = ttft_point - start_time
            gen_duration = max(end_time - ttft_point, 0.001)
            total_duration = end_time - start_time

            # Оценка токенов, если провайдер не вернул usageMetadata / usage
            if out_tokens == 0 and accumulated_text:
                full_text = "".join(accumulated_text)
                out_tokens = max(1, int(len(full_text) / 3.5))

            total_tokens = out_tokens + thought_tokens
            pure_tps = (out_tokens / gen_duration) if gen_duration > 0 else 0.0
            eff_tps = (total_tokens / total_duration) if total_duration > 0 else 0.0

            tok_str = f"{out_tokens}" + (f" (+{thought_tokens})" if thought_tokens else "")

            table.add_row(
                prov.get("name", "Unknown"),
                model,
                f"{ttft:.2f} сек",
                f"{gen_duration:.2f} сек",
                tok_str,
                f"{pure_tps:.2f} т/с",
                f"{eff_tps:.2f} т/с"
            )

            self.results_history.append({
                "timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
                "provider": prov.get("name", "Unknown"),
                "endpoint": self.active_endpoint,
                "model": model,
                "ttft_sec": round(ttft, 3),
                "stream_duration_sec": round(gen_duration, 3),
                "total_duration_sec": round(total_duration, 3),
                "output_tokens": out_tokens,
                "thought_tokens": thought_tokens,
                "pure_tps": round(pure_tps, 2),
                "effective_tps": round(eff_tps, 2),
            })

            status.update(
                f"[+] [{prov.get('name')}] {model} | TTFT: {ttft:.2f} с | Чистый TPS: {pure_tps:.2f} т/с | Экспорт: 'e'"
            )

        except Exception as e:
            status.update(f"[!] Ошибка запроса к {model}: {e}")
        finally:
            run_btn.disabled = False


if __name__ == "__main__":
    LLMBenchTUI().run()