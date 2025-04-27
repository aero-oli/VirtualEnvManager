using System;

namespace VirtualEnvManager.Services
{
    public class SettingsService
    {
        public string GetWorkonHome()
            => Environment.GetEnvironmentVariable("WORKON_HOME") ?? "";

        public void SetWorkonHome(string path)
            => Environment.SetEnvironmentVariable("WORKON_HOME", path, EnvironmentVariableTarget.User);
    }
} 