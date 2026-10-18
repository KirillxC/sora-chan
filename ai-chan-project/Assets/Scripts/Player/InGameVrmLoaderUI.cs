using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UniVRM10;
using AiChan.Spinal;

namespace AiChan.Player
{
    /// <summary>
    /// In-game runtime VRM Loader UI.
    /// Allows loading any VRM model directly during gameplay:
    /// - Wear as Player Avatar (adjusts camera to eye level, CharacterController, shadows)
    /// - Spawn as Companion / AI-Chan with automatic active ragdoll joint rigging (RuntimeRagdollRigger).
    /// Hotkeys: [U] or [F1] to toggle UI, or click top-right [📁 VRM] button.
    /// </summary>
    public class InGameVrmLoaderUI : MonoBehaviour
    {
        [Header("Settings")]
        public bool showOnStart = false;
        public KeyCode toggleKey = KeyCode.U;
        public KeyCode alternateToggleKey = KeyCode.F1;

        private bool _isOpen = false;
        private string _customPathInput = "";
        private List<string> _discoveredVrms = new List<string>();
        private string _statusMessage = "";
        private bool _isLoading = false;
        private Vector2 _scrollPos = Vector2.zero;

        private PlayerController _playerController;

        private void Start()
        {
            _playerController = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();
            _isOpen = showOnStart;
            ScanDiscoveredVrmFiles();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(alternateToggleKey))
            {
                _isOpen = !_isOpen;
                if (_isOpen)
                {
                    ScanDiscoveredVrmFiles();
                }
            }
        }

        public void ScanDiscoveredVrmFiles()
        {
            _discoveredVrms.Clear();
            var searchedDirs = new List<string>();

            // 1. Assets/Models
            string assetsModels = Path.Combine(Application.dataPath, "Models");
            searchedDirs.Add(assetsModels);

            // 2. Project root / models
            string projectModels = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "models"));
            searchedDirs.Add(projectModels);

            // 3. User Home / Downloads / Desktop
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(homeDir))
            {
                searchedDirs.Add(Path.Combine(homeDir, "Downloads"));
                searchedDirs.Add(Path.Combine(homeDir, "Desktop"));
                searchedDirs.Add(homeDir);
            }

            foreach (var dir in searchedDirs)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        var files = Directory.GetFiles(dir, "*.vrm", SearchOption.TopDirectoryOnly);
                        foreach (var f in files)
                        {
                            if (!_discoveredVrms.Contains(f))
                            {
                                _discoveredVrms.Add(f);
                            }
                        }
                    }
                }
                catch { }
            }

            if (_discoveredVrms.Count > 0 && string.IsNullOrEmpty(_customPathInput))
            {
                _customPathInput = _discoveredVrms[0];
            }
        }

        private void OnGUI()
        {
            // Sleek toggle button in top-right corner (always accessible when cursor is free)
            int btnWidth = 140;
            int btnHeight = 34;
            int margin = 12;
            Rect toggleRect = new Rect(Screen.width - btnWidth - margin, margin, btnWidth, btnHeight);

            GUI.color = _isOpen ? new Color(1f, 0.6f, 0.7f, 0.95f) : new Color(0.15f, 0.2f, 0.35f, 0.85f);
            if (GUI.Button(toggleRect, _isOpen ? "✕ Закрыть меню" : "📁 Загрузить VRM"))
            {
                _isOpen = !_isOpen;
                if (_isOpen) ScanDiscoveredVrmFiles();
            }
            GUI.color = Color.white;

            // Small hint under button
            Rect hintRect = new Rect(Screen.width - 240 - margin, margin + btnHeight + 4, 240, 22);
            GUI.Label(hintRect, "<size=11><color=#88bbff>[U] или [F1] — меню VRM</color></size>");

            if (!_isOpen) return;

            // Translucent Dark Anime Window
            int winWidth = Mathf.Min(640, Screen.width - 40);
            int winHeight = Mathf.Min(560, Screen.height - 60);
            int winX = (Screen.width - winWidth) / 2;
            int winY = (Screen.height - winHeight) / 2;

            Rect winRect = new Rect(winX, winY, winWidth, winHeight);

            // Window background
            GUI.Box(winRect, "");
            GUI.Box(winRect, ""); // Double box for dark opacity

            GUILayout.BeginArea(new Rect(winX + 15, winY + 15, winWidth - 30, winHeight - 30));

            // Header
            GUILayout.Label("<size=18><b>✨ Загрузчик VRM-моделей в реальном времени</b></size>");
            GUILayout.Label("<size=12><color=#cccccc>Загружайте любого персонажа прямо в игре. Суставы и физика настраиваются автоматически.</color></size>");
            GUILayout.Space(8);

            // Controls summary banner
            GUI.color = new Color(0.2f, 0.3f, 0.5f, 0.4f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.color = Color.white;
            GUILayout.Label("<size=11><color=#aaccff>🎮 <b>Управление:</b> [ПКМ] — обзор мышью | [CapsLock] — заблокировать мышь | [WASD] — ходьба | [Shift] — бег | [V] — вид 1-е/3-е лицо</color></size>");
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Path Input Section
            GUILayout.Label("<b>Укажите путь к файлу .vrm на диске:</b>");
            GUILayout.BeginHorizontal();
            _customPathInput = GUILayout.TextField(_customPathInput, GUILayout.Height(28));
            if (GUILayout.Button("🔄 Обновить", GUILayout.Width(90), GUILayout.Height(28)))
            {
                ScanDiscoveredVrmFiles();
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_customPathInput) && File.Exists(_customPathInput))
            {
                GUILayout.BeginHorizontal();
                GUI.color = new Color(0.3f, 0.85f, 0.5f, 1f);
                if (GUILayout.Button("👤 Надеть на себя (Аватар игрока)", GUILayout.Height(32)))
                {
                    LoadVrmModel(_customPathInput, loadAsPlayer: true);
                }
                GUI.color = new Color(0.4f, 0.7f, 1f, 1f);
                if (GUILayout.Button("🤖 Заспавнить как AI-Chan (Авто-суставы)", GUILayout.Height(32)))
                {
                    LoadVrmModel(_customPathInput, loadAsPlayer: false);
                }
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(12);

            // Discovered VRM Models List
            GUILayout.Label($"<b>Найденные VRM-файлы в системе ({_discoveredVrms.Count}):</b>");

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(170));
            if (_discoveredVrms.Count == 0)
            {
                GUILayout.Label("<color=#888888>Файлы .vrm не найдены в стандартных папках. Введите полный путь выше.</color>");
            }
            else
            {
                foreach (var path in _discoveredVrms)
                {
                    string filename = Path.GetFileName(path);
                    string dir = Path.GetDirectoryName(path);

                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label($"<b>{filename}</b> <color=#777777>({dir})</color>");

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Выбрать путь", GUILayout.Width(110)))
                    {
                        _customPathInput = path;
                    }
                    GUI.color = new Color(0.3f, 0.85f, 0.5f, 1f);
                    if (GUILayout.Button("👤 Надеть на себя", GUILayout.Width(160)))
                    {
                        LoadVrmModel(path, loadAsPlayer: true);
                    }
                    GUI.color = new Color(0.4f, 0.7f, 1f, 1f);
                    if (GUILayout.Button("🤖 Заспавнить AI-Chan", GUILayout.Width(170)))
                    {
                        LoadVrmModel(path, loadAsPlayer: false);
                    }
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();

                    GUILayout.EndVertical();
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8);

            // Status message / progress
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUI.color = _statusMessage.StartsWith("ERROR") ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 1f, 0.5f);
                GUILayout.Label($"<b>{_statusMessage}</b>");
                GUI.color = Color.white;
            }

            if (_isLoading)
            {
                GUILayout.Label("<i>⏳ Загрузка и процедурная расстановка скелета...</i>");
            }

            GUILayout.EndArea();
        }

        public async void LoadVrmModel(string vrmPath, bool loadAsPlayer)
        {
            if (_isLoading) return;
            if (!File.Exists(vrmPath))
            {
                _statusMessage = $"ERROR: Файл не найден: {vrmPath}";
                return;
            }

            _isLoading = true;
            _statusMessage = $"Загрузка {Path.GetFileName(vrmPath)}...";

            try
            {
                // Asynchronously load VRM 1.0 / 0.x via UniVRM
                var vrmInstance = await Vrm10.LoadPathAsync(vrmPath);
                if (vrmInstance == null)
                {
                    _statusMessage = "ERROR: UniVRM не смог разобрать файл модели.";
                    _isLoading = false;
                    return;
                }

                GameObject avatarGo = vrmInstance.gameObject;
                avatarGo.name = Path.GetFileNameWithoutExtension(vrmPath);

                if (loadAsPlayer)
                {
                    // 1. Attach to PlayerController
                    if (_playerController == null)
                    {
                        _playerController = FindFirstObjectByType<PlayerController>();
                    }

                    if (_playerController != null)
                    {
                        _playerController.AttachAvatarModel(avatarGo);
                        _statusMessage = $"✅ Модель '{avatarGo.name}' успешно надета на вашего аватара!";
                    }
                    else
                    {
                        _statusMessage = "ERROR: PlayerController не найден в сцене.";
                    }
                }
                else
                {
                    // 2. Spawn / Replace as AI-Chan with Active Ragdoll Joints
                    var existingAvatar = GameObject.Find("AiChan_Avatar");
                    if (existingAvatar != null)
                    {
                        avatarGo.transform.position = existingAvatar.transform.position;
                        avatarGo.transform.rotation = existingAvatar.transform.rotation;
                        Destroy(existingAvatar);
                    }
                    else
                    {
                        avatarGo.transform.position = Vector3.zero;
                        avatarGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Face player
                    }

                    avatarGo.name = "AiChan_Avatar";

                    // Automatically rig all joints, limits, springs, colliders!
                    _statusMessage = "Расстановка суставов, пружин и коллайдеров...";
                    bool ok = RuntimeRagdollRigger.RigActiveRagdoll(avatarGo, pinHips: true);

                    // Add LookAt and Thalamus
                    if (avatarGo.GetComponent<AiChan.Avatar.Gaze.AvatarLookAtController>() == null)
                    {
                        avatarGo.AddComponent<AiChan.Avatar.Gaze.AvatarLookAtController>();
                    }

                    if (ok)
                    {
                        _statusMessage = $"✅ Модель '{avatarGo.name}' заспавнена! Все 15 физических суставов расставлены.";
                    }
                    else
                    {
                        _statusMessage = "⚠️ Модель загружена, но у неё нет гуманоидного скелета.";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InGameVrmLoaderUI] Error loading VRM: {ex}");
                _statusMessage = $"ERROR: {ex.Message}";
            }
            finally
            {
                _isLoading = false;
            }
        }
    }
}
