using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace FsocietyCleaner
{
    /// <summary>
    /// Reads real hardware identifiers and generates fake ones (display only).
    /// Used by the Spoof tab (Before/After comparison) and the System Info tab.
    /// </summary>
    public class HardwareInfo
    {
        public string Uuid       { get; set; } = "—";
        public string BiosSerial { get; set; } = "—";
        public string DiskSerial { get; set; } = "—";
        public string MacAddress { get; set; } = "—";
        public string MoboSerial { get; set; } = "—";

        // ─── Read the real current hardware IDs ──────────────────────────

        public static HardwareInfo ReadCurrent() => new()
        {
            Uuid       = WmicGet("csproduct", "uuid"),
            BiosSerial = WmicGet("bios",      "serialnumber"),
            DiskSerial = ReadAllDiskSerials().FirstOrDefault() ?? "—",
            MacAddress = ReadAllMacAddresses().FirstOrDefault().mac ?? "—",
            MoboSerial = WmicGet("baseboard", "serialnumber"),
        };

        // ─── Extended details for the System Info tab ────────────────────

        public static (string osName, string machine, string user, string cpu,
                       string ram, string biosVendor, string mobo)
            ReadDetails()
        {
            string osName = ReadOsName();
            string machine = SafeGet(() => Environment.MachineName);
            string user    = SafeGet(() => Environment.UserName);
            string cpu     = ReadCpu();
            string ram     = ReadRam();
            string biosVendor = WmicGet("bios", "manufacturer");
            string moboMan = WmicGet("baseboard", "manufacturer");
            string moboProd = WmicGet("baseboard", "product");
            string mobo = $"{Compact(moboMan)} {Compact(moboProd)}".Trim();
            if (string.IsNullOrWhiteSpace(mobo)) mobo = "—";

            return (osName, machine, user, cpu, ram, biosVendor, mobo);
        }

        public static List<string> ReadAllDiskSerials()
        {
            var results = new List<string>();
            try
            {
                string raw = WmicRaw("diskdrive get serialnumber,model /value");
                var entries = raw.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var entry in entries)
                {
                    var modelMatch  = Regex.Match(entry, "Model=(.+)", RegexOptions.IgnoreCase);
                    var serialMatch = Regex.Match(entry, "SerialNumber=(.+)", RegexOptions.IgnoreCase);
                    string model = modelMatch.Success  ? modelMatch.Groups[1].Value.Trim() : "";
                    string sn    = serialMatch.Success ? serialMatch.Groups[1].Value.Trim() : "";
                    if (string.IsNullOrWhiteSpace(sn)) continue;
                    results.Add(string.IsNullOrWhiteSpace(model) ? sn : $"{sn}   ({model})");
                }
            }
            catch { }
            return results;
        }

        public static List<(string mac, string name)> ReadAllMacAddresses()
        {
            var results = new List<(string, string)>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)   continue;

                    byte[] bytes = ni.GetPhysicalAddress().GetAddressBytes();
                    if (bytes.Length != 6) continue;

                    string mac = string.Join(":", bytes.Select(b => b.ToString("X2")));
                    if (mac == "00:00:00:00:00:00") continue;

                    string typeLabel = ni.NetworkInterfaceType switch
                    {
                        NetworkInterfaceType.Ethernet              => "Ethernet",
                        NetworkInterfaceType.Wireless80211         => "WiFi",
                        NetworkInterfaceType.GigabitEthernet       => "Ethernet (Gb)",
                        NetworkInterfaceType.FastEthernetT         => "Ethernet",
                        _                                          => ni.NetworkInterfaceType.ToString(),
                    };
                    results.Add((mac, typeLabel));
                }
            }
            catch { }
            return results;
        }

        // ─── Generate fake values for display only ───────────────────────

        public static HardwareInfo GenerateFake() => new()
        {
            Uuid       = Guid.NewGuid().ToString().ToUpperInvariant(),
            BiosSerial = RandomAlphaNumeric(10),
            DiskSerial = RandomAlphaNumeric(20),
            MacAddress = RandomMac(),
            MoboSerial = RandomAlphaNumeric(12),
        };

        // ─── Internals ───────────────────────────────────────────────────

        private static readonly Random _rng = new();

        private static string RandomAlphaNumeric(int len)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var arr = new char[len];
            for (int i = 0; i < len; i++) arr[i] = chars[_rng.Next(chars.Length)];
            return new string(arr);
        }

        private static string RandomMac()
        {
            var b = new byte[6];
            _rng.NextBytes(b);
            b[0] = (byte)((b[0] & 0xFE) | 0x02); // locally administered, unicast
            return string.Join(":", b.Select(x => x.ToString("X2")));
        }

        private static string ReadOsName()
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                string product = k?.GetValue("ProductName") as string ?? "Windows";
                string version = k?.GetValue("DisplayVersion") as string ?? "";
                string build   = k?.GetValue("CurrentBuild")   as string ?? "?";
                // Windows 11 still reports "Windows 10" in ProductName — patch it
                if (int.TryParse(build, out int bn) && bn >= 22000 && product.Contains("Windows 10"))
                    product = product.Replace("Windows 10", "Windows 11");
                return $"{product} {version}".Trim() + $"  (Build {build})";
            }
            catch { return Environment.OSVersion.ToString(); }
        }

        private static string ReadCpu()
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return Compact(k?.GetValue("ProcessorNameString") as string ?? "—");
            }
            catch { return "—"; }
        }

        private static string ReadRam()
        {
            string raw = WmicGet("computersystem", "totalphysicalmemory");
            if (long.TryParse(raw, out long bytes) && bytes > 0)
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
            return "—";
        }

        private static string WmicGet(string alias, string property)
        {
            try
            {
                string raw = WmicRaw($"{alias} get {property} /value");
                var m = Regex.Match(raw, $"{property}=(.+)", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    string val = m.Groups[1].Value.Trim();
                    return string.IsNullOrWhiteSpace(val) ? "—" : val;
                }
            }
            catch { }
            return "—";
        }

        private static string WmicRaw(string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wmic.exe",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return "";
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                return output;
            }
            catch { return ""; }
        }

        private static string Compact(string s) =>
            Regex.Replace(s ?? "", @"\s+", " ").Trim();

        private static string SafeGet(Func<string> f)
        {
            try { return f() ?? "—"; }
            catch { return "—"; }
        }
    }
}
