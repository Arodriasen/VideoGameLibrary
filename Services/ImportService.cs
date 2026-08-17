using VideoGameLibrary.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ClosedXML.Excel;

namespace VideoGameLibrary.Services
{
    public enum ImportItemStatus { Nuevo, YaExiste, DuplicadoEnArchivo }

    // Lee un CSV (separado por ";", mismo formato que ExportService) o un Excel (.xlsx)
    // con una fila de cabecera. Las columnas se identifican por nombre (no por posición),
    // así que el usuario puede omitir u ordenar las columnas como quiera.
    // Única columna obligatoria: "Título".
    public class ImportService
    {
        private static readonly string[] RecognizedHeaders =
        {
            "código de barras", "título", "plataforma", "editorial", "género", "año", "puntuación", "notas", "jugado"
        };

        public static IReadOnlyList<string> HeaderNames => RecognizedHeaders;

        public List<Game> ParseFile(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".csv", System.StringComparison.OrdinalIgnoreCase)
                ? ParseCsv(filePath)
                : ParseExcel(filePath);
        }

        private List<Game> ParseCsv(string filePath)
        {
            var result = new List<Game>();
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            if (lines.Length == 0) return result;

            int start = lines[0].TrimStart().StartsWith("sep=", System.StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            if (start >= lines.Length) return result;

            var map = BuildHeaderMap(SplitCsvLine(lines[start]));
            if (!map.ContainsKey("título")) return result;

            for (int i = start + 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var fields = SplitCsvLine(lines[i]);
                var game = BuildGame(field => GetField(fields, map, field));
                if (game != null) result.Add(game);
            }

            return result;
        }

        private List<Game> ParseExcel(string filePath)
        {
            var result = new List<Game>();
            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheets.First();

            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var headers = new List<string>();
            for (int col = 1; col <= lastCol; col++)
                headers.Add(sheet.Cell(1, col).GetString());

            var map = BuildHeaderMap(headers);
            if (!map.ContainsKey("título")) return result;

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (int row = 2; row <= lastRow; row++)
            {
                int currentRow = row;
                var game = BuildGame(field =>
                    map.TryGetValue(field, out var col) ? sheet.Cell(currentRow, col + 1).GetString() : null);
                if (game != null) result.Add(game);
            }

            return result;
        }

        // ── Construcción de un Game a partir de un lector de campos por nombre de columna ──

        private static Game? BuildGame(System.Func<string, string?> field)
        {
            var title = field("título")?.Trim();
            if (string.IsNullOrWhiteSpace(title)) return null;

            var game = new Game { Title = title };

            var barcode = field("código de barras")?.Trim();
            game.Barcode = string.IsNullOrWhiteSpace(barcode) ? null : GameApiService.NormalizeBarcode(barcode);

            game.Platform = field("plataforma")?.Trim() ?? string.Empty;
            game.Publisher = field("editorial")?.Trim() ?? string.Empty;
            game.Genre = field("género")?.Trim() ?? string.Empty;
            game.Notes = field("notas")?.Trim() ?? string.Empty;
            game.Year = ParseYear(field("año"));
            game.Rating = ParseRating(field("puntuación"));
            game.Played = ParsePlayed(field("jugado"));

            return game;
        }

        private static int? ParseYear(string? text)
            => int.TryParse(text, out var y) && y >= 1970 && y <= 2100 ? y : null;

        private static int ParseRating(string? text)
            => int.TryParse(text, out var r) && r >= 0 && r <= 5 ? r : 0;

        private static bool ParsePlayed(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var v = text.Trim().ToLowerInvariant();
            return v is "sí" or "si" or "yes" or "1" or "true" or "x";
        }

        // ── Vista previa: clasifica cada fila del archivo antes de importar nada ───────────
        // "Ya existe" compara por código de barras contra la colección actual, o por título+plataforma
        // cuando la fila no trae código de barras (el índice único de la BD solo cubre el barcode).
        // "Duplicado en el archivo" detecta repeticiones dentro del propio archivo importado.
        public static List<(Game Game, ImportItemStatus Status)> BuildPreview(List<Game> parsed, List<Game> existing)
        {
            var existingBarcodes = new HashSet<string>(
                existing.Where(g => !string.IsNullOrEmpty(g.Barcode)).Select(g => g.Barcode!),
                StringComparer.OrdinalIgnoreCase);
            var existingTitleKeys = new HashSet<string>(existing.Select(g => TitleKey(g.Title, g.Platform)));

            var seenBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenTitleKeys = new HashSet<string>();

            var result = new List<(Game, ImportItemStatus)>();
            foreach (var game in parsed)
            {
                var status = Classify(game, existingBarcodes, existingTitleKeys, seenBarcodes, seenTitleKeys);
                result.Add((game, status));
            }
            return result;
        }

        private static ImportItemStatus Classify(Game game, HashSet<string> existingBarcodes,
            HashSet<string> existingTitleKeys, HashSet<string> seenBarcodes, HashSet<string> seenTitleKeys)
        {
            if (!string.IsNullOrEmpty(game.Barcode))
            {
                if (existingBarcodes.Contains(game.Barcode)) return ImportItemStatus.YaExiste;
                return seenBarcodes.Add(game.Barcode) ? ImportItemStatus.Nuevo : ImportItemStatus.DuplicadoEnArchivo;
            }

            var titleKey = TitleKey(game.Title, game.Platform);
            if (existingTitleKeys.Contains(titleKey)) return ImportItemStatus.YaExiste;
            return seenTitleKeys.Add(titleKey) ? ImportItemStatus.Nuevo : ImportItemStatus.DuplicadoEnArchivo;
        }

        private static string TitleKey(string title, string platform) =>
            $"{title.Trim().ToLowerInvariant()}|{platform.Trim().ToLowerInvariant()}";

        // ── Cabeceras: se identifican por nombre, no por posición ──────────────

        private static Dictionary<string, int> BuildHeaderMap(List<string> headers)
        {
            var map = new Dictionary<string, int>();
            for (int i = 0; i < headers.Count; i++)
            {
                var key = headers[i].Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                    map[key] = i;
            }
            return map;
        }

        private static string? GetField(List<string> fields, Dictionary<string, int> map, string header)
            => map.TryGetValue(header, out var idx) && idx < fields.Count ? fields[idx] : null;

        // ── CSV: separado por ";", con comillas dobles para escapar (mismo formato que ExportService) ──

        private static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else current.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ';') { fields.Add(current.ToString()); current.Clear(); }
                    else current.Append(c);
                }
            }
            fields.Add(current.ToString());
            return fields;
        }
    }
}
