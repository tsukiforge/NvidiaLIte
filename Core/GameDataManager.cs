using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace NvidiaCi
{
    public class GameDataManager
    {
        private readonly string _filePath;

        public GameDataManager()
        {
            // Menentukan path: C:\Users\<User>\AppData\Local\NvidiaCi\games.json
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folderPath = Path.Combine(appDataPath, "NvidiaCi");
            
            // Pastikan folder sudah dibuat
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            _filePath = Path.Combine(folderPath, "games.json");
        }

        public void SaveGames(List<GameItem> games)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(games, options);
            string temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, jsonString);
            File.Move(temporaryPath, _filePath, true);
        }

        public List<GameItem> LoadGames()
        {
            if (!File.Exists(_filePath))
                return new List<GameItem>();

            try
            {
                string jsonString = File.ReadAllText(_filePath);
                var games = JsonSerializer.Deserialize<List<GameItem>>(jsonString) ?? new List<GameItem>();
                return games.FindAll(game =>
                    !string.IsNullOrWhiteSpace(game.Name) &&
                    !string.IsNullOrWhiteSpace(game.Platform) &&
                    File.Exists(game.Path));
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Saved game list is invalid ({_filePath}): {ex.Message}");
                throw new InvalidDataException($"Saved game list is invalid: {_filePath}", ex);
            }
        }
    }
}
