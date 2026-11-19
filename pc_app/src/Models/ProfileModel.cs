using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GamepadEmulator.Models
{
    public class GamepadProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "新規プロファイル";

        // 旧プロパティ互換（保護制限は撤廃され、全プロファイルが自由に変更・削除可能）
        public bool IsProtected { get; set; } = false;

        // MODE切替（false: D-Pad出力 / true: 左スティック出力）
        public bool IsStickMode { get; set; } = false;

        // 旧プロパティとの後方互換用
        [JsonIgnore]
        public bool ModeOn
        {
            get => IsStickMode;
            set => IsStickMode = value;
        }

        // ボタン名 -> 仮想キーコード(VK)のリスト（1つのボタンに複数キーを登録可能）
        // 例: "Up" => [0x57 (W), 0x20 (Space)]
        public Dictionary<string, List<int>> Bindings { get; set; } = new();

        public static GamepadProfile CreateDefault(string name = "デフォルト")
        {
            return new GamepadProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                IsProtected = false,
                IsStickMode = false,
                Bindings = new Dictionary<string, List<int>>
                {
                    { "Up", new List<int> { 0x57, 0x20 } }, // W と Space
                    { "Down", new List<int> { 0x53 } },     // S
                    { "Left", new List<int> { 0x41 } },     // A
                    { "Right", new List<int> { 0x44 } },    // D
                    { "A", new List<int> { 0x4A } },        // J
                    { "B", new List<int> { 0x4B } },        // K
                    { "X", new List<int> { 0x55 } },        // U
                    { "Y", new List<int> { 0x49 } },        // I
                    { "LB", new List<int>() },
                    { "RB", new List<int>() },
                    { "LT", new List<int>() },
                    { "RT", new List<int>() },
                    { "Start", new List<int>() },
                    { "Back", new List<int>() },
                    { "L3", new List<int>() },
                    { "R3", new List<int>() },
                    { "Guide", new List<int>() }
                }
            };
        }

        public GamepadProfile Clone()
        {
            var clone = new GamepadProfile
            {
                Id = this.Id,
                Name = this.Name,
                IsProtected = false,
                IsStickMode = this.IsStickMode,
                Bindings = new Dictionary<string, List<int>>()
            };
            foreach (var kvp in this.Bindings)
            {
                clone.Bindings[kvp.Key] = new List<int>(kvp.Value);
            }
            return clone;
        }

        public GamepadProfile Clone(string newName)
        {
            var clone = Clone();
            clone.Id = Guid.NewGuid().ToString();
            clone.Name = newName;
            clone.IsProtected = false;
            return clone;
        }

        public override string ToString() => Name;
    }

    public class ProfileStorage
    {
        public string ActiveProfileId { get; set; } = string.Empty;
        public List<GamepadProfile> Profiles { get; set; } = new();
    }

    public class ProfileManager
    {
        private readonly string _storagePath;
        private ProfileStorage _storage = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public ProfileManager(string? customPath = null)
        {
            _storagePath = customPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles.json");
            LoadProfiles();
        }

        public string StoragePath => _storagePath;

        public IReadOnlyList<GamepadProfile> Profiles => _storage.Profiles;

        public GamepadProfile? ActiveProfile
        {
            get
            {
                if (_storage.Profiles.Count == 0)
                {
                    _storage.ActiveProfileId = string.Empty;
                    return null;
                }

                var profile = _storage.Profiles.FirstOrDefault(p => p.Id == _storage.ActiveProfileId);
                if (profile == null)
                {
                    profile = _storage.Profiles[0];
                    _storage.ActiveProfileId = profile.Id;
                }
                return profile;
            }
            set
            {
                if (value == null)
                {
                    _storage.ActiveProfileId = string.Empty;
                    return;
                }
                var found = _storage.Profiles.FirstOrDefault(p => p.Id == value.Id);
                if (found != null)
                {
                    _storage.ActiveProfileId = found.Id;
                    SaveProfiles();
                }
            }
        }

        public void LoadProfiles()
        {
            if (!File.Exists(_storagePath))
            {
                CreateAndSaveInitialDefault();
                return;
            }

            try
            {
                string json = File.ReadAllText(_storagePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var storage = new ProfileStorage();
                if (root.TryGetProperty("ActiveProfileId", out var activeIdElem))
                {
                    storage.ActiveProfileId = activeIdElem.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("Profiles", out var profilesElem) && profilesElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pElem in profilesElem.EnumerateArray())
                    {
                        var profile = new GamepadProfile();

                        if (pElem.TryGetProperty("Id", out var idElem))
                            profile.Id = idElem.GetString() ?? Guid.NewGuid().ToString();

                        if (pElem.TryGetProperty("Name", out var nameElem))
                            profile.Name = nameElem.GetString() ?? "新規プロファイル";

                        profile.IsProtected = false; // 全プロファイル保護撤廃

                        // IsStickMode / ModeOn の両対応
                        if (pElem.TryGetProperty("IsStickMode", out var stickElem))
                            profile.IsStickMode = stickElem.GetBoolean();
                        else if (pElem.TryGetProperty("ModeOn", out var modeOnElem))
                            profile.IsStickMode = modeOnElem.GetBoolean();

                        // Bindings のデシリアライズ（旧 Dictionary<string, int> および新 Dictionary<string, List<int>> の両方に対応）
                        profile.Bindings = new Dictionary<string, List<int>>();
                        if (pElem.TryGetProperty("Bindings", out var bindingsElem) && bindingsElem.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in bindingsElem.EnumerateObject())
                            {
                                var list = new List<int>();
                                if (prop.Value.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in prop.Value.EnumerateArray())
                                    {
                                        if (item.TryGetInt32(out int vk) && vk > 0 && !list.Contains(vk))
                                        {
                                            list.Add(vk);
                                        }
                                    }
                                }
                                else if (prop.Value.ValueKind == JsonValueKind.Number)
                                {
                                    if (prop.Value.TryGetInt32(out int vk) && vk > 0)
                                    {
                                        list.Add(vk);
                                    }
                                }
                                profile.Bindings[prop.Name] = list;
                            }
                        }

                        storage.Profiles.Add(profile);
                    }
                }

                // 0件の場合は0件のまま安全に読み込み（自動再生成しない）
                _storage = storage;

                if (_storage.Profiles.Count == 0)
                {
                    _storage.ActiveProfileId = string.Empty;
                }
                else if (string.IsNullOrWhiteSpace(_storage.ActiveProfileId) ||
                    !_storage.Profiles.Any(p => p.Id == _storage.ActiveProfileId))
                {
                    _storage.ActiveProfileId = _storage.Profiles[0].Id;
                }
            }
            catch (Exception)
            {
                BackupAndRecoverWithDefault();
            }
        }

        private void BackupAndRecoverWithDefault()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    string backupPath = _storagePath + ".bak";
                    File.Copy(_storagePath, backupPath, overwrite: true);
                }
            }
            catch
            {
                // Best effort backup
            }

            CreateAndSaveInitialDefault();
        }

        private void CreateAndSaveInitialDefault()
        {
            var defaultProfile = GamepadProfile.CreateDefault("デフォルト");
            _storage = new ProfileStorage
            {
                ActiveProfileId = defaultProfile.Id,
                Profiles = new List<GamepadProfile> { defaultProfile }
            };
            SaveProfiles();
        }

        public void SaveProfiles()
        {
            try
            {
                string json = JsonSerializer.Serialize(_storage, JsonOptions);
                string directory = Path.GetDirectoryName(_storagePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Atomic save via temp file
                string tempPath = _storagePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _storagePath, overwrite: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save profiles: {ex.Message}");
            }
        }

        public bool IsProfileNameExists(string name, string? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var trimmed = name.Trim();
            return _storage.Profiles.Any(p =>
                (excludeId == null || p.Id != excludeId) &&
                string.Equals(p.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        }

        private string ValidateAndTrimNewProfileName(string name, string paramName)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("プロファイル名を空にすることはできません。", paramName);
            }

            string trimmedName = name.Trim();
            if (IsProfileNameExists(trimmedName))
            {
                throw new ArgumentException($"既に同名のプロファイル '{trimmedName}' が存在します。", paramName);
            }

            return trimmedName;
        }

        public GamepadProfile CreateProfile(string name)
        {
            string trimmedName = ValidateAndTrimNewProfileName(name, nameof(name));

            // 過去の設定や現在のActiveProfileに依存せず、常に完全な新規インスタンス（標準初期バインド）として生成
            var newProfile = GamepadProfile.CreateDefault(trimmedName);

            _storage.Profiles.Add(newProfile);
            _storage.ActiveProfileId = newProfile.Id;
            SaveProfiles();
            return newProfile;
        }

        /// <summary>
        /// 指定されたプロファイルをディープコピーし、新しい名前で登録・JSON永続化する。
        /// </summary>
        /// <param name="source">複製元のプロファイル</param>
        /// <param name="newName">複製先の新しいプロファイル名（前後空白トリム・空文字/重複検証対象）</param>
        /// <returns>作成・永続化された新しい GamepadProfile インスタンス</returns>
        public GamepadProfile DuplicateProfile(GamepadProfile source, string newName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            string trimmedName = ValidateAndTrimNewProfileName(newName, nameof(newName));
            var duplicated = source.Clone(trimmedName);

            _storage.Profiles.Add(duplicated);
            _storage.ActiveProfileId = duplicated.Id;
            SaveProfiles();
            return duplicated;
        }

        public bool DeleteProfile(string id)
        {
            var target = _storage.Profiles.FirstOrDefault(p => p.Id == id);
            if (target == null) return false;

            int index = _storage.Profiles.IndexOf(target);
            bool wasActive = _storage.ActiveProfileId == id;
            _storage.Profiles.RemoveAt(index);

            if (_storage.Profiles.Count == 0)
            {
                _storage.ActiveProfileId = string.Empty;
            }
            else if (wasActive)
            {
                int newActiveIndex = Math.Min(index, _storage.Profiles.Count - 1);
                _storage.ActiveProfileId = _storage.Profiles[newActiveIndex].Id;
            }

            // メモリ上から消すだけでなく、即座にファイル（profiles.json）へ書き込み完全抹消
            SaveProfiles();
            return true;
        }

        public bool RenameProfile(string id, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return false;

            var profile = _storage.Profiles.FirstOrDefault(p => p.Id == id);
            if (profile == null) return false;

            string trimmed = newName.Trim();

            // 自身の現在の名前と同じ（大文字小文字・前後の空白トリム後）なら成功扱い
            if (string.Equals(profile.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                if (profile.Name != trimmed)
                {
                    profile.Name = trimmed;
                    SaveProfiles();
                }
                return true;
            }

            // 自身以外のプロファイルとの重複チェック
            if (IsProfileNameExists(trimmed, id))
            {
                return false;
            }

            profile.Name = trimmed;
            SaveProfiles();
            return true;
        }
    }

    [Obsolete("Use GamepadProfile and ProfileManager instead.")]
    public class ProfileModel
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool ModeOn { get; set; } = false;
        public bool GuideLocked { get; set; } = true;
        public string? TargetProcessName { get; set; }

        public Dictionary<int, GamepadButton> KeyBindings { get; set; } = new();

        public static Dictionary<int, GamepadButton> CreateDefaultKeyBindings()
        {
            return new Dictionary<int, GamepadButton>
            {
                { 0x57, GamepadButton.Up },
                { 0x53, GamepadButton.Down },
                { 0x41, GamepadButton.Left },
                { 0x44, GamepadButton.Right },
                { 0x4A, GamepadButton.A },
                { 0x4B, GamepadButton.B },
                { 0x55, GamepadButton.X },
                { 0x49, GamepadButton.Y },
                { 0x4C, GamepadButton.LB },
                { 0x4F, GamepadButton.RB },
                { 0x4E, GamepadButton.L3 },
                { 0x4D, GamepadButton.R3 },
                { 0x20, GamepadButton.LT },
                { 0x48, GamepadButton.RT },
                { 0x0D, GamepadButton.Start },
                { 0x08, GamepadButton.Back },
                { 0x1B, GamepadButton.Guide }
            };
        }

        public override string ToString() => Name;
    }
}
