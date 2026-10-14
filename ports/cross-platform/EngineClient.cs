using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CraftConsole.CrossPlatform
{
    public static class EngineClient
    {
        public const int ControlPort = 9877;

        public static string? SendTcpCommand(string json, int timeoutMs = 4000)
        {
            try
            {
                using var client = new TcpClient();
                var connectResult = client.BeginConnect("127.0.0.1", ControlPort, null, null);
                if (!connectResult.AsyncWaitHandle.WaitOne(timeoutMs))
                {
                    return null;
                }
                client.EndConnect(connectResult);

                using var stream = client.GetStream();
                stream.ReadTimeout = timeoutMs;
                stream.WriteTimeout = timeoutMs;

                var utf8 = new UTF8Encoding(false);
                using var reader = new StreamReader(stream, utf8);
                using var writer = new StreamWriter(stream, utf8) { AutoFlush = true };

                writer.WriteLine(json);
                return reader.ReadLine();
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string?> SendTcpCommandAsync(string json, int timeoutMs = 4000)
        {
            return await Task.Run(() => SendTcpCommand(json, timeoutMs));
        }

        public static bool IsEngineListening(int timeoutMs = 250)
        {
            try
            {
                using var client = new TcpClient();
                var connectResult = client.BeginConnect("127.0.0.1", ControlPort, null, null);
                if (!connectResult.AsyncWaitHandle.WaitOne(timeoutMs))
                {
                    return false;
                }
                client.EndConnect(connectResult);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<(bool success, string message)> ApplyItemAsync(EffectItem item)
        {
            return await Task.Run(() =>
            {
                string json;
                if (item.Kind == ConsoleItemKind.Preset)
                {
                    json = "{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"layer.applyTextPreset\",\"params\":{\"preset\":\"" + item.Id + "\"}}}";
                }
                else if (item.Kind == ConsoleItemKind.Layer)
                {
                    if (item.Id == "layer.newSolid")
                    {
                        json = "{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"layer.newSolid\",\"params\":{\"color\":\"#ffffff\"}}}";
                    }
                    else
                    {
                        json = "{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"" + item.Id + "\",\"params\":{}}}";
                    }
                }
                else if (item.Kind == ConsoleItemKind.Command)
                {
                    json = "{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"" + item.Id + "\",\"params\":{}}}";
                }
                else if (item.Kind == ConsoleItemKind.Project)
                {
                    json = "{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"file.open\",\"params\":{\"path\":\"" + item.Id.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}}}";
                    string? openRes = SendTcpCommand(json);
                    
                    // Auto-dismiss Home screen overlay so the opened project is visible
                    try
                    {
                        SendTcpCommand("{\"id\":11,\"method\":\"ui.set\",\"params\":{\"home\":false}}");
                    }
                    catch { }

                    return (true, "Project opened");
                }
                else
                {
                    // Effect
                    string effectParam = (!string.IsNullOrEmpty(item.Id) && item.Id.StartsWith("ec.")) ? item.Id : item.Name;
                    json = "{\"id\":10,\"method\":\"ui.menu.invoke\",\"params\":{\"id\":\"effect.apply\",\"params\":{\"effect\":\"" + effectParam + "\"}}}";
                }

                string? res = SendTcpCommand(json);
                if (string.IsNullOrEmpty(res))
                {
                    return (false, "Cannot connect to EffectCraft (port 9877)");
                }

                if (res.IndexOf("no layer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return (false, "No layer selected in EffectCraft");
                }

                if (res.IndexOf("no comp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    res.IndexOf("no active comp", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return (false, "No active composition in EffectCraft");
                }

                if (res.Contains("\"error\":"))
                {
                    string errMsg = "Effect could not be applied";
                    try
                    {
                        using var doc = JsonDocument.Parse(res);
                        if (doc.RootElement.TryGetProperty("error", out var errProp))
                        {
                            if (errProp.ValueKind == JsonValueKind.String)
                                errMsg = errProp.GetString() ?? errMsg;
                            else if (errProp.TryGetProperty("message", out var msgProp))
                                errMsg = msgProp.GetString() ?? errMsg;
                        }
                    }
                    catch { }
                    return (false, errMsg);
                }

                return (true, "Applied successfully");
            });
        }

        public static void SyncLiveMenuItems(Action<List<EffectItem>> onItemsFound)
        {
            Task.Run(() =>
            {
                try
                {
                    string json = "{\"id\":5,\"method\":\"ui.menu.list\",\"params\":{}}";
                    string? res = SendTcpCommand(json);
                    if (string.IsNullOrEmpty(res) || !res.Contains("\"result\":")) return;

                    var discovered = new List<EffectItem>();
                    using var doc = JsonDocument.Parse(res);
                    if (doc.RootElement.TryGetProperty("result", out var resultProp))
                    {
                        if (resultProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var el in resultProp.EnumerateArray())
                            {
                                string name = el.TryGetProperty("name", out var np) ? (np.GetString() ?? "") : "";
                                string id = el.TryGetProperty("id", out var ip) ? (ip.GetString() ?? "") : "";
                                string cat = el.TryGetProperty("category", out var cp) ? (cp.GetString() ?? "Command") : "Command";
                                string shortcut = el.TryGetProperty("shortcut", out var sp) ? (sp.GetString() ?? "") : "";

                                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id))
                                {
                                    discovered.Add(new EffectItem
                                    {
                                        Name = name,
                                        Id = id,
                                        Category = cat,
                                        Kind = ConsoleItemKind.Command,
                                        Extra = shortcut,
                                        LowerName = name.ToLowerInvariant(),
                                        LowerCategory = cat.ToLowerInvariant()
                                    });
                                }
                            }
                        }
                    }

                    if (discovered.Count > 0)
                    {
                        onItemsFound(discovered);
                    }
                }
                catch { }
            });
        }
    }
}
