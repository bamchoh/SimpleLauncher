using System.Diagnostics;

namespace SimpleLauncher.Services
{
    public sealed class ProcessLauncher : IProcessLauncher
    {
        public void Start(ProcessStartInfo startInfo)
        {
            Process.Start(startInfo)?.Dispose();
        }
    }
}
