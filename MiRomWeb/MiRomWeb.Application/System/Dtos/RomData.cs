using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace XiaomiLib;
public class FullRomData
{
    public List<RomEntry> Fastboot { get; set; }
    public List<RomEntry> Recovery { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("FullRomData Tree Structure:");
        sb.AppendLine(".");

        if (Fastboot == null || Fastboot.Count == 0)
        {
            sb.AppendLine("└── <no data>");
        }
        else
        {
            var groupedByDevice = Fastboot
                .Where(r => r != null)
                .GroupBy(r => r.Device ?? "(unknown device)")
                .ToDictionary(g => g.Key, g => g.ToList());

            sb.AppendLine("└── fastboot/");
            PrintGroupedDeviceEntries(sb, groupedByDevice, "    ");
        }

        return sb.ToString();
    }

    private void PrintGroupedDeviceEntries(StringBuilder sb, Dictionary<string, List<RomEntry>> groupedEntries, string parentIndent)
    {
        var keys = new List<string>(groupedEntries.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            string device = keys[i];
            bool isLast = i == keys.Count - 1;
            string prefix = isLast ? "└── " : "├── ";
            string nextIndent = parentIndent + (isLast ? "    " : "│   ");

            sb.AppendLine($"{parentIndent}{prefix}{device}/");

            var roms = groupedEntries[device];
            if (roms != null && roms.Count > 0)
            {
                for (int j = 0; j < roms.Count; j++)
                {
                    bool romIsLast = j == roms.Count - 1;
                    string romPrefix = romIsLast ? "└── " : "├── ";
                    var rom = roms[j];

                    string fileNameDisplay = rom.FileName ?? "(no filename)";
                    sb.AppendLine($"{nextIndent}{romPrefix}{fileNameDisplay}");

                    string detailIndent = nextIndent + (romIsLast ? "    " : "│   ");

                    // 打印非空字段
                    PrintIfNotNull(sb, detailIndent, "Type", rom.Type);
                    PrintIfNotNull(sb, detailIndent, "RomID", rom.RomID.ToString());
                    PrintIfNotNull(sb, detailIndent, "Version", rom.Version);
                    PrintIfNotNull(sb, detailIndent, "OS Version", rom.OSVersion);
                    PrintIfNotNull(sb, detailIndent, "Device", rom.Device);
                    PrintIfNotNull(sb, detailIndent, "CodeBase", rom.CodeBase);
                    PrintIfNotNull(sb, detailIndent, "FileSize", rom.FileSize);
                    PrintIfNotNull(sb, detailIndent, "MD5", rom.MD5);
                    PrintIfNotNull(sb, detailIndent, "SHA1", rom.SHA1);
                    PrintIfNotNull(sb, detailIndent, "BigVersion", rom.BigVersion);
                    PrintIfNotNull(sb, detailIndent, "OSBigVersion", rom.OSBigVersion);
                    PrintIfNotNull(sb, detailIndent, "PublicLevel", rom.PublicLevel);
                    PrintIfNotNull(sb, detailIndent, "DownloadVersion", rom.DownloadVersion);
                    foreach (var url in rom.DownloadUrls)
                    {
                        PrintIfNotNull(sb, detailIndent, "DownloadUrl", url);
                    }
                    if (!romIsLast)
                    {
                        sb.AppendLine(nextIndent + "│");
                    }
                }
            }
            else
            {
                sb.AppendLine($"{nextIndent}└── <no roms>");
            }
        }
    }

    private void PrintIfNotNull(StringBuilder sb, string indent, string label, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            sb.AppendLine($"{indent}{label}: {value}");
        }
    }
}
public class RomData
{
    [JsonProperty("xmRecovery")]
    [JsonPropertyName("xmRecovery")]
    public Dictionary<string, List<RomEntry>> Recovery { get; set; }
    [JsonProperty("xmFastboot")]
    [JsonPropertyName("xmFastboot")]
    public Dictionary<string, List<RomEntry>> Fastboot { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("RomData Tree Structure:");
        sb.AppendLine(".");

        bool hasRecovery = Recovery != null && Recovery.Count > 0;
        bool hasFastboot = Fastboot != null && Fastboot.Count > 0;

        if (hasRecovery)
        {
            sb.AppendLine("├── recovery/");
            PrintDeviceEntries(sb, Recovery, "    ");
        }

        if (hasFastboot)
        {
            if (hasRecovery)
            {
                sb.AppendLine(hasFastboot ? "├── fastboot/" : "└── fastboot/");
                PrintDeviceEntries(sb, Fastboot, "    ");
            }
            else
            {
                sb.AppendLine("├── fastboot/");
                PrintDeviceEntries(sb, Fastboot, "    ");
            }
        }

        if (!hasRecovery && !hasFastboot)
        {
            sb.AppendLine("└── <no data>");
        }

        return sb.ToString();
    }

    private void PrintDeviceEntries(StringBuilder sb, Dictionary<string, List<RomEntry>> entries, string parentIndent)
    {
        var keys = new List<string>(entries.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            string device = keys[i];
            bool isLast = i == keys.Count - 1;
            string prefix = isLast ? "└── " : "├── ";
            string nextIndent = parentIndent + (isLast ? "    " : "│   ");

            sb.AppendLine($"{parentIndent}{prefix}{device}/");

            var roms = entries[device];
            if (roms != null && roms.Count > 0)
            {
                for (int j = 0; j < roms.Count; j++)
                {
                    bool romIsLast = j == roms.Count - 1;
                    string romPrefix = romIsLast ? "└── " : "├── ";
                    var rom = roms[j];

                    string fileNameDisplay = rom.FileName ?? $"(no filename)";
                    sb.AppendLine($"{nextIndent}{romPrefix}{fileNameDisplay}");

                    // 详细信息缩进
                    string detailIndent = nextIndent + (romIsLast ? "    " : "│   ");

                    // 只输出非 null/非空字段
                    PrintIfNotNull(sb, detailIndent, "Type", rom.Type);
                    PrintIfNotNull(sb, detailIndent, "RomID", rom.RomID.ToString());
                    PrintIfNotNull(sb, detailIndent, "Version", rom.Version);
                    PrintIfNotNull(sb, detailIndent, "OS Version", rom.OSVersion);
                    PrintIfNotNull(sb, detailIndent, "Device", rom.Device);
                    PrintIfNotNull(sb, detailIndent, "CodeBase", rom.CodeBase);
                    PrintIfNotNull(sb, detailIndent, "FileSize", rom.FileSize);
                    PrintIfNotNull(sb, detailIndent, "MD5", rom.MD5);
                    PrintIfNotNull(sb, detailIndent, "SHA1", rom.SHA1);
                    PrintIfNotNull(sb, detailIndent, "BigVersion", rom.BigVersion);
                    PrintIfNotNull(sb, detailIndent, "OSBigVersion", rom.OSBigVersion);
                    PrintIfNotNull(sb, detailIndent, "PublicLevel", rom.PublicLevel);
                    PrintIfNotNull(sb, detailIndent, "DownloadVersion", rom.DownloadVersion);
                    foreach(var url in rom.DownloadUrls)
                    {
                        PrintIfNotNull(sb, detailIndent, "DownloadUrl", url);
                    }

                    // 每个 ROM 后加一个空行分隔（可选）
                    if (!romIsLast)
                    {
                        sb.AppendLine(nextIndent + "│");
                    }
                }
            }
            else
            {
                sb.AppendLine($"{nextIndent}└── <no roms>");
            }
        }
    }

    private void PrintIfNotNull(StringBuilder sb, string indent, string label, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            sb.AppendLine($"{indent}{label}: {value}");
        }
    }
}
public class RomEntry
{
    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public string Type { get; set; }
    [JsonProperty("romID")]
    [JsonPropertyName("romID")]
    public int RomID { get; set; }
    [JsonProperty("ver")]
    [JsonPropertyName("ver")]
    public string Version { get; set; }
    [JsonProperty("osVersion")]
    [JsonPropertyName("osVersion")]
    public string OSVersion { get; set; }
    [JsonProperty("device")]
    [JsonPropertyName("device")]
    public string Device { get; set; }
    [JsonProperty("codebase")]
    [JsonPropertyName("codebase")]
    public string CodeBase { get; set; }
    [JsonProperty("fileName")]
    [JsonPropertyName("fileName")]
    public string FileName { get; set; }
    [JsonProperty("fileSize")]
    [JsonPropertyName("fileSize")]
    public string FileSize { get; set; }
    [JsonProperty("md5")]
    [JsonPropertyName("md5")]
    public string MD5 { get; set; }
    [JsonProperty("sha1")]
    [JsonPropertyName("sha1")]
    public string SHA1 { get; set; }
    [JsonProperty("bigVersion")]
    [JsonPropertyName("bigVersion")]
    public string BigVersion { get; set; }
    [JsonProperty("osBigVersion")]
    [JsonPropertyName("osBigVersion")]
    public string OSBigVersion { get; set; }
    [JsonProperty("pubLevel")]
    [JsonPropertyName("pubLevel")]
    public string PublicLevel { get; set; }
    [JsonProperty("downloadVer")]
    [JsonPropertyName("downloadVer")]
    public string DownloadVersion { get; set; }

    [JsonProperty("urls")]
    [JsonPropertyName("urls")]
    public List<string> Mirrors { get; set; }
    public IEnumerable<string> DownloadUrls
    {
        get
        {
            if (Mirrors.Count > 0 && DownloadVersion?.Length > 0 && FileName?.Length > 0)
            {
                
                return Mirrors.Select(it => $"{it}/{DownloadVersion}/{FileName}");
            }
            return [];
        }
    }
    public RomEntry()
    {

    }
}