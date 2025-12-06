using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace LobbyFileTests
{
    internal static class MapData
    {
        // Avoid conflict with System.IO.FileInfo by calling this MapFile
        public record MapFile(string File, string Nick, string Tag);

        public static readonly Dictionary<string, MapFile> FileMap = new();
        public static MapUser User = new(string.Empty, string.Empty);

        public record MapUser(string Nick, string Tag);

        static MapData()
        {
            var testDir = TestContext.CurrentContext.TestDirectory;
            var mapPathPossible = Path.Combine(testDir, "..", "..", "..", "lobbyfiles.map.json");
            var mapPath = Path.GetFullPath(mapPathPossible);
            if (!File.Exists(mapPath)) mapPath = Path.Combine(testDir, "lobbyfiles.map.json");
            if (!File.Exists(mapPath)) throw new FileNotFoundException("lobbyfiles.map.json was not found in output nor source folder.");
            var json = File.ReadAllText(mapPath);

            // Fast-path: try to parse as a whole JSON document
            JsonDocument? wholeDoc = null;
            try
            {
                wholeDoc = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                wholeDoc = null;
            }

            // Fallback: try to extract a 'user' object anywhere in the file, even if JSON is malformed
            var userIndex = json.IndexOf("\"user\"", StringComparison.OrdinalIgnoreCase);
            if (userIndex >= 0)
            {
                var braceIndex = json.IndexOf('{', userIndex);
                if (braceIndex >= 0)
                {
                    int depth = 0; int endIdx = -1;
                    for (int i = braceIndex; i < json.Length; i++)
                    {
                        if (json[i] == '{') depth++;
                        else if (json[i] == '}') { depth--; if (depth == 0) { endIdx = i; break; } }
                    }
                    if (endIdx > braceIndex)
                    {
                        try
                        {
                            var userObjStr = json.Substring(braceIndex, endIdx - braceIndex + 1);
                            using var udoc = JsonDocument.Parse(userObjStr);
                            var userObjEl = udoc.RootElement;
                            var nick = string.Empty; var tag = string.Empty;
                            if (userObjEl.TryGetProperty("nick", out var nickEl) && nickEl.ValueKind == JsonValueKind.String) nick = nickEl.GetString() ?? string.Empty;
                            if (userObjEl.TryGetProperty("tag", out var tagEl) && tagEl.ValueKind == JsonValueKind.String) tag = tagEl.GetString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(nick) || !string.IsNullOrEmpty(tag))
                                User = new MapUser(nick, tag);
                        }
                        catch (JsonException) { /* ignore malformed user object */ }
                    }
                }
            }

            int pos = 0;
            string? candidateJson = null;
            while (pos < json.Length)
            {
                int firstBraceLocal = json.IndexOf('{', pos);
                if (firstBraceLocal < 0) break;
                int braceCountLocal = 0;
                int endIndexLocal = -1;
                for (int i = firstBraceLocal; i < json.Length; i++)
                {
                    if (json[i] == '{') braceCountLocal++;
                    else if (json[i] == '}') braceCountLocal--;
                    if (braceCountLocal == 0) { endIndexLocal = i; break; }
                }
                if (endIndexLocal < 0) break;
                var candidate = json.Substring(firstBraceLocal, endIndexLocal - firstBraceLocal + 1);
                // Validate it's an object with 'testfiles'
                try
                {
                    using var doc = JsonDocument.Parse(candidate);
                    if (doc.RootElement.TryGetProperty("testfiles", out var _))
                    {
                        candidateJson = candidate;
                        break;
                    }
                }
                catch (JsonException) { /* try next */ }
                pos = endIndexLocal + 1;
            }

            JsonElement root;
            // If wholeDoc is null and we didn't find a candidate, try tolerant array parsing directly
            if (wholeDoc == null && string.IsNullOrEmpty(candidateJson))
            {
                var tfIndex = json.IndexOf("\"testfiles\"", StringComparison.OrdinalIgnoreCase);
                if (tfIndex >= 0)
                {
                    var bracketStart = json.IndexOf('[', tfIndex);
                    if (bracketStart >= 0)
                    {
                        int depth = 0; int end = -1;
                        for (int i = bracketStart; i < json.Length; i++)
                        {
                            if (json[i] == '[') depth++;
                            else if (json[i] == ']') { depth--; if (depth == 0) { end = i; break; } }
                        }
                        if (end > bracketStart)
                        {
                            var arrayContent = json.Substring(bracketStart + 1, end - bracketStart - 1);
                            // Walk content char by char, extracting string tokens and object tokens
                            string? pendingFileLocal = null;
                            for (int i = 0; i < arrayContent.Length; i++)
                            {
                                if (arrayContent[i] == '"')
                                {
                                    int start = i + 1; int j = start; bool escape = false;
                                    for (; j < arrayContent.Length; j++)
                                    {
                                        if (arrayContent[j] == '\\') { escape = !escape; continue; }
                                        if (arrayContent[j] == '"' && !escape) break;
                                        escape = false;
                                    }
                                    var str = arrayContent.Substring(start, j - start);
                                    i = j; // advance
                                    if (!string.IsNullOrEmpty(str) && !FileMap.ContainsKey(str))
                                    {
                                        FileMap[str] = new MapFile(str, string.Empty, string.Empty);
                                    }
                                    pendingFileLocal = str;
                                    continue;
                                }
                                if (arrayContent[i] == '{')
                                {
                                    int depthObj = 0; int j = i;
                                    for (; j < arrayContent.Length; j++)
                                    {
                                        if (arrayContent[j] == '{') depthObj++;
                                        else if (arrayContent[j] == '}') depthObj--;
                                        if (depthObj == 0) break;
                                    }
                                    if (j >= arrayContent.Length) break;
                                    var objStr = arrayContent.Substring(i, j - i + 1);
                                    i = j;
                                    try
                                    {
                                        using var doc = JsonDocument.Parse(objStr);
                                        var rootObj = doc.RootElement;
                                        string? file = null; string? nick = null; string? tag = null;
                                        if (rootObj.TryGetProperty("file", out var fileEl) && fileEl.ValueKind == JsonValueKind.String) file = fileEl.GetString();
                                        if (rootObj.TryGetProperty("nick", out var nickEl) && nickEl.ValueKind == JsonValueKind.String) nick = nickEl.GetString();
                                        if (rootObj.TryGetProperty("tag", out var tagEl) && tagEl.ValueKind == JsonValueKind.String) tag = tagEl.GetString();

                                        if (string.IsNullOrEmpty(file)) file = pendingFileLocal;
                                        if (string.IsNullOrEmpty(file)) continue;
                                        FileMap[file] = new MapFile(file, nick ?? string.Empty, tag ?? string.Empty);
                                        pendingFileLocal = null;
                                    }
                                    catch (JsonException) { /* ignore bad object */ }
                                    continue;
                                }
                            }
                            // finished tolerant parse
                            return;
                        }
                    }
                }
            }
            if (wholeDoc != null)
            {
                root = wholeDoc.RootElement;
            }
            else
            {
                if (string.IsNullOrEmpty(candidateJson))
                    throw new InvalidDataException("Could not find a valid map JSON object with 'testfiles' property in lobbyfiles.map.json");
                using var rootDoc = JsonDocument.Parse(candidateJson);
                root = rootDoc.RootElement;
            }
            if (root.TryGetProperty("user", out var userEl))
            {
                var nick = string.Empty;
                var tag = string.Empty;
                if (userEl.TryGetProperty("nick", out var nickEl)) nick = nickEl.GetString() ?? string.Empty;
                if (userEl.TryGetProperty("tag", out var tagEl)) tag = tagEl.GetString() ?? string.Empty;
                User = new MapUser(nick, tag);
            }

            if (!root.TryGetProperty("testfiles", out var arrEl) || arrEl.ValueKind != JsonValueKind.Array)
            {
                // Try a tolerant parsing of the array substring if the root object isn't standard JSON
                var tfIndex = json.IndexOf("\"testfiles\"", StringComparison.OrdinalIgnoreCase);
                if (tfIndex >= 0)
                {
                    var bracketStart = json.IndexOf('[', tfIndex);
                    if (bracketStart >= 0)
                    {
                        int depth = 0; int end = -1;
                        for (int i = bracketStart; i < json.Length; i++)
                        {
                            if (json[i] == '[') depth++;
                            else if (json[i] == ']') { depth--; if (depth == 0) { end = i; break; } }
                        }
                        if (end > bracketStart)
                        {
                            var arrayContent = json.Substring(bracketStart + 1, end - bracketStart - 1);
                            // Walk content char by char, extracting string tokens and object tokens
                            string? pendingFileLocal = null;
                            for (int i = 0; i < arrayContent.Length; i++)
                            {
                                if (arrayContent[i] == '"')
                                {
                                    // parse string
                                    int start = i + 1; int j = start; bool escape = false;
                                    for (; j < arrayContent.Length; j++)
                                    {
                                        if (arrayContent[j] == '\\') { escape = !escape; continue; }
                                        if (arrayContent[j] == '"' && !escape) break;
                                        escape = false;
                                    }
                                    var str = arrayContent.Substring(start, j - start);
                                    i = j; // advance
                                    if (!string.IsNullOrEmpty(str) && !FileMap.ContainsKey(str))
                                    {
                                        FileMap[str] = new MapFile(str, string.Empty, string.Empty);
                                    }
                                    pendingFileLocal = str;
                                    continue;
                                }
                                if (arrayContent[i] == '{')
                                {
                                    // find matching object
                                    int depthObj = 0; int j = i;
                                    for (; j < arrayContent.Length; j++)
                                    {
                                        if (arrayContent[j] == '{') depthObj++;
                                        else if (arrayContent[j] == '}') depthObj--;
                                        if (depthObj == 0) break;
                                    }
                                    if (j >= arrayContent.Length) break;
                                    var objStr = arrayContent.Substring(i, j - i + 1);
                                    i = j;
                                    try
                                    {
                                        using var doc = JsonDocument.Parse(objStr);
                                        var rootObj = doc.RootElement;
                                        string? file = null; string? nick = null; string? tag = null;
                                        if (rootObj.TryGetProperty("file", out var fileEl) && fileEl.ValueKind == JsonValueKind.String) file = fileEl.GetString();
                                        if (rootObj.TryGetProperty("nick", out var nickEl) && nickEl.ValueKind == JsonValueKind.String) nick = nickEl.GetString();
                                        if (rootObj.TryGetProperty("tag", out var tagEl) && tagEl.ValueKind == JsonValueKind.String) tag = tagEl.GetString();

                                        if (string.IsNullOrEmpty(file)) file = pendingFileLocal;
                                        if (string.IsNullOrEmpty(file)) continue;
                                        FileMap[file] = new MapFile(file, nick ?? string.Empty, tag ?? string.Empty);
                                        pendingFileLocal = null;
                                    }
                                    catch (JsonException) { /* ignore bad object */ }
                                    continue;
                                }
                            }
                            // finished tolerant parse
                        }
                    }
                }
                return;
            }

            string? pendingFile = null;
            foreach (var item in arrEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    pendingFile = item.GetString();
                    if (!string.IsNullOrEmpty(pendingFile) && !FileMap.ContainsKey(pendingFile))
                    {
                        FileMap[pendingFile] = new MapFile(pendingFile, string.Empty, string.Empty);
                    }
                    continue;
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    string? file = null;
                    string? nick = null;
                    string? tag = null;
                    if (item.TryGetProperty("file", out var fileEl) && fileEl.ValueKind == JsonValueKind.String) file = fileEl.GetString();
                    if (item.TryGetProperty("nick", out var nickEl) && nickEl.ValueKind == JsonValueKind.String) nick = nickEl.GetString();
                    if (item.TryGetProperty("tag", out var tagEl) && tagEl.ValueKind == JsonValueKind.String) tag = tagEl.GetString();

                    if (string.IsNullOrEmpty(file)) file = pendingFile;
                    if (string.IsNullOrEmpty(file)) continue;
                    FileMap[file] = new MapFile(file, nick ?? string.Empty, tag ?? string.Empty);
                    pendingFile = null;
                    continue;
                }
            }
        }

        public static (string Nick, string Tag)? TryGetExpected(string file)
        {
            if (FileMap.TryGetValue(file, out var info) && (!string.IsNullOrEmpty(info.Nick) || !string.IsNullOrEmpty(info.Tag)))
            {
                return (info.Nick, info.Tag);
            }
            return null;
        }
    }
}
