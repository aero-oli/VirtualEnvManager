using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using VirtualEnvManager.Models;

namespace VirtualEnvManager.Services
{
    public class VirtualEnvService
    {
        private readonly string workonHome;

        public VirtualEnvService(string workonHomePath)
        {
            workonHome = workonHomePath;
            if (!string.IsNullOrEmpty(workonHome) && !Directory.Exists(workonHome))
            {
                try
                {
                    Directory.CreateDirectory(workonHome);
                }
                catch (Exception ex)
                {
                    throw new DirectoryNotFoundException($"Failed to create or access WORKON_HOME: {workonHome}", ex);
                }
            }
            else if (string.IsNullOrEmpty(workonHome))
            {
                throw new ArgumentException("WORKON_HOME path cannot be null or empty.", nameof(workonHomePath));
            }
        }

        public string WorkonHome => workonHome;

        public List<VirtualEnv> ListEnvs()
        {
            if (!Directory.Exists(workonHome))
            {
                Console.Error.WriteLine($"WORKON_HOME directory not found or inaccessible: {workonHome}");
                return new List<VirtualEnv>();
            }

            var envs = new List<VirtualEnv>();
            try
            {
                foreach (var dir in Directory.GetDirectories(workonHome))
                {
                    var name = Path.GetFileName(dir);
                    var pyExe = Path.Combine(dir, "Scripts", "python.exe");
                    string pyVersion = "";
                    if (File.Exists(pyExe))
                    {
                        var psi = new ProcessStartInfo(pyExe, "--version")
                        {
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var p = Process.Start(psi);
                        pyVersion = p.StandardOutput.ReadToEnd().Trim();
                        p.WaitForExit();
                    }

                    envs.Add(new VirtualEnv
                    {
                        Name = name,
                        Path = dir,
                        PythonVersion = pyVersion,
                        SizeBytes = GetDirectorySize(dir)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error listing environments: {ex.Message}");
            }
            return envs;
        }

        private long GetDirectorySize(string dir)
        {
            return Directory
                .GetFiles(dir, "*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f).Length)
                .Sum();
        }

        public void CreateEnv(string name, string pythonInterpreter = null)
        {
            // Construct the mkvirtualenv command
            string command = "mkvirtualenv";

            // Add the python interpreter flag if specified
            if (!string.IsNullOrEmpty(pythonInterpreter))
            {
                command += $" -p \"{pythonInterpreter}\"";
            }

            // Add the environment name
            command += $" \"{name}\"";

            // Execute using the wrapper command helper
            ExecuteWrapperCommand(command); 
        }

        public void DeleteEnv(string name)
            => ExecuteWrapperCommand($"rmvirtualenv \"{name}\"");

        public void CloneEnv(string source, string target)
            => ExecuteWrapperCommand($"cpvirtualenv \"{source}\" \"{target}\"");

        public List<PackageInfo> ListPackages(string envName)
        {
            var pip = Path.Combine(workonHome, envName, "Scripts", "pip.exe");
            if (!File.Exists(pip))
                throw new FileNotFoundException("pip not found");
            var psi = new ProcessStartInfo(pip, "list --format=json")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var outp = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return JsonConvert.DeserializeObject<List<PackageInfo>>(outp);
        }

        public void InstallPackage(string envName, string pkg)
            => RunPip(envName, $"install {pkg}");

        public void RemovePackage(string envName, string pkg)
            => RunPip(envName, $"uninstall -y {pkg}");

        public void UpgradePackage(string envName, string pkg)
            => RunPip(envName, $"install --upgrade {pkg}");

        private void RunPip(string envName, string args)
        {
            var pip = Path.Combine(workonHome, envName, "Scripts", "pip.exe");
            if (!File.Exists(pip))
                throw new FileNotFoundException("pip not found");
            RunCmd(pip, args);
        }

        public Dictionary<string, string> GetEnvVars(string envName)
        {
            var activate = Path.Combine(workonHome, envName, "Scripts", "activate.bat");
            var dict = new Dictionary<string, string>();
            if (File.Exists(activate))
            {
                foreach (var line in File.ReadAllLines(activate))
                {
                    if (line.TrimStart().StartsWith("set "))
                    {
                        var parts = line.Trim().Substring(4).Split('=', 2);
                        if (parts.Length == 2)
                            dict[parts[0]] = parts[1];
                    }
                }
            }
            return dict;
        }

        public void SetEnvVar(string envName, string key, string value)
        {
            var activate = Path.Combine(workonHome, envName, "Scripts", "activate.bat");
            var lines = File.Exists(activate)
                ? File.ReadAllLines(activate).ToList()
                : new List<string>();
            lines.RemoveAll(l => l.TrimStart().StartsWith($"set {key}="));
            lines.Add($"set {key}={value}");
            File.WriteAllLines(activate, lines);
        }

        public void RemoveEnvVar(string envName, string key)
        {
            var activate = Path.Combine(workonHome, envName, "Scripts", "activate.bat");
            if (!File.Exists(activate)) return;
            var lines = File.ReadAllLines(activate)
                .Where(l => !l.TrimStart().StartsWith($"set {key}="))
                .ToList();
            File.WriteAllLines(activate, lines);
        }

        public void ExecuteWrapperCommand(string cmd)
            => RunCmd("cmd.exe", $"/c {cmd}");

        private void RunCmd(string file, string args)
        {
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute   = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow    = true,
                WorkingDirectory  = workonHome
            };
            Environment.SetEnvironmentVariable("WORKON_HOME", workonHome);
            using var p = Process.Start(psi);
            p.WaitForExit();
            if (p.ExitCode != 0)
                throw new Exception(p.StandardError.ReadToEnd());
        }
    }
} 