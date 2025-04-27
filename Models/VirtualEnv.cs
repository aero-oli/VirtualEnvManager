namespace VirtualEnvManager.Models
{
    public class VirtualEnv
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string PythonVersion { get; set; }
        public long SizeBytes { get; set; }
    }
} 