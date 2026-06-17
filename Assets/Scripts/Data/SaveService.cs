using System;
using System.IO;
using UnityEngine;
using YoonseulFishing.Core;

namespace YoonseulFishing.Data
{
    /// <summary>
    /// JSON-file implementation of <see cref="ISaveService"/> (Phase 2). Serializes
    /// the persisted slice of <see cref="GameState"/> (via <see cref="SaveData"/>)
    /// to a single file under <c>Application.persistentDataPath</c>.
    ///
    /// Boot wiring:
    /// <code>
    ///   var state = new GameState();
    ///   var save  = new SaveService(state);
    ///   save.Load();                                  // JSON → state (no-op if no file yet)
    ///   var ctrl  = new GameController(state, save, audio);
    /// </code>
    /// After that, <see cref="GameController"/> calls <see cref="Save"/> at each
    /// progress-changing point, exactly where the Android build hit prefs/Room.
    ///
    /// Both Save and Load are defensive: any IO/parse failure is logged and
    /// swallowed rather than throwing into gameplay (a corrupt or missing file
    /// must never crash the game — it simply starts from defaults).
    /// </summary>
    public class SaveService : ISaveService
    {
        private const string FileName = "save.json";

        private readonly GameState _state;
        private readonly string _filePath;

        /// <param name="state">The live game state to snapshot / restore.</param>
        /// <param name="filePath">Override the save path (used by tests); defaults to
        /// <c>persistentDataPath/save.json</c>.</param>
        public SaveService(GameState state, string filePath = null)
        {
            _state = state;
            _filePath = filePath ?? Path.Combine(Application.persistentDataPath, FileName);
        }

        /// <summary>Absolute path of the save file.</summary>
        public string FilePath => _filePath;

        public void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(SaveData.FromState(_state), prettyPrint: true);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Save failed: {e}");
            }
        }

        /// <summary>
        /// Restores state from disk. No-op (state keeps its defaults) when the file
        /// is absent or unreadable. Not part of <see cref="ISaveService"/> — only the
        /// boot path needs it.
        /// </summary>
        public void Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return;
                string json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json)) return;

                var data = JsonUtility.FromJson<SaveData>(json);
                data?.ApplyTo(_state);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Load failed; starting from defaults: {e}");
            }
        }

        /// <summary>Deletes the save file (e.g. a full reset). Safe if it doesn't exist.</summary>
        public void DeleteSaveFile()
        {
            try
            {
                if (File.Exists(_filePath)) File.Delete(_filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Delete failed: {e}");
            }
        }
    }
}
