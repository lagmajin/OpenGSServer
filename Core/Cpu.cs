using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace OpenGSServer
{
    static class Cpu
    {
        public static string ArchitectureName()
        {
            return RuntimeInformation.ProcessArchitecture.ToString();
        }

        public static int LogicalCoreCount()
        {
            return Environment.ProcessorCount;
        }

        public static string CpuCoreCount()
        {
            return Environment.ProcessorCount.ToString();
        }

        public static string CpuVendor()
        {
            // x86/x64: CPUID命令で取得
            if (X86Base.IsSupported)
            {
                return GetVendorFromCpuId();
            }

            // その他: OS別の方法で取得
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetVendorWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetVendorLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetVendorMacOS();
            }

            return "Unknown";
        }

        private static string GetVendorFromCpuId()
        {
            // CPUID EAX=0 でベンダーID取得
            var (_, ebx, ecx, edx) = X86Base.CpuId(0, 0);
            
            Span<byte> vendorBytes = stackalloc byte[12];
            BitConverter.TryWriteBytes(vendorBytes[0..4], ebx);
            BitConverter.TryWriteBytes(vendorBytes[4..8], edx);
            BitConverter.TryWriteBytes(vendorBytes[8..12], ecx);

            return System.Text.Encoding.ASCII.GetString(vendorBytes);
        }

        private static string GetVendorWindows()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "cpu get manufacturer",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return "Unknown";

                string output = process.StandardOutput.ReadToEnd();
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                return lines.Length > 1 ? lines[1].Trim() : "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string GetVendorLinux()
        {
            try
            {
                if (!File.Exists("/proc/cpuinfo")) return "Unknown";

                foreach (var line in File.ReadLines("/proc/cpuinfo"))
                {
                    if (line.StartsWith("vendor_id", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            return parts[1].Trim();
                        }
                    }
                }
            }
            catch
            {
            }
            return "Unknown";
        }

        private static string GetVendorMacOS()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "-n machdep.cpu.vendor",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return "Unknown";

                return process.StandardOutput.ReadToEnd().Trim();
            }
            catch
            {
                // Apple Silicon (ARM) の場合
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    return "Apple";
                }
                return "Unknown";
            }
        }

        public static string GetCpuInfo()
        {
            return $"Architecture: {ArchitectureName()}, Cores: {CpuCoreCount()}, Vendor: {CpuVendor()}";
        }
    }
}
