using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using DiagnosticsProcess = System.Diagnostics.Process;

namespace InstallerLauncher.Infrastructure.Process;

[SupportedOSPlatform("windows")]
public sealed class JobObjectManager : IDisposable
{
    private readonly IntPtr _jobHandle;
    private readonly ConcurrentDictionary<int, DiagnosticsProcess> _trackedProcesses = new();
    private bool _disposed;

    public JobObjectManager()
    {
        _jobHandle = CreateJobObject(IntPtr.Zero, null);
        if (_jobHandle == IntPtr.Zero)
        {
            ThrowLastWin32Error("Unable to create job object");
        }

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            }
        };

        var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var infoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, infoPtr, false);
            if (!SetInformationJobObject(_jobHandle, JobObjectInfoType.ExtendedLimitInformation, infoPtr, (uint)length))
            {
                ThrowLastWin32Error("Unable to configure job object limits");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
        }
    }

    public void AddProcess(DiagnosticsProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);
        EnsureNotDisposed();

        if (!AssignProcessToJobObject(_jobHandle, process.Handle))
        {
            ThrowLastWin32Error($"Failed to assign process {process.ProcessName} ({process.Id}) to job");
        }

        _trackedProcesses[process.Id] = process;
    }

    public IReadOnlyCollection<int> TrackedProcessIds => _trackedProcesses.Keys.ToArray();

    private void ThrowLastWin32Error(string message)
    {
        throw new Win32Exception(Marshal.GetLastWin32Error(), message);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(JobObjectManager));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CloseHandle(_jobHandle);
        foreach (var process in _trackedProcesses.Values)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~JobObjectManager()
    {
        Dispose();
    }

    #region Native Interop

    private const int JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    private enum JobObjectInfoType
    {
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public int LimitFlags;
        public IntPtr MinimumWorkingSetSize;
        public IntPtr MaximumWorkingSetSize;
        public int ActiveProcessLimit;
        public long Affinity;
        public int PriorityClass;
        public int SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public IntPtr ProcessMemoryLimit;
        public IntPtr JobMemoryLimit;
        public IntPtr PeakProcessMemoryUsed;
        public IntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    #endregion
}
