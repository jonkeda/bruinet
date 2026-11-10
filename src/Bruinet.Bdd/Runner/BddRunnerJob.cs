using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Bruinet.Bdd.Runner;

/// <summary>
/// A Windows job that ends its processes when its host does, however it ends: a runner, the app it
/// started and its mock server never outlive a host that crashed (AD-025). Elsewhere it holds
/// nothing, and a runner ends when its pipe closes.
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed class BddRunnerJob : IDisposable
{
    private const int ExtendedLimitInformation = 9;
    private const uint KillOnJobClose = 0x2000;
    private readonly SafeFileHandle? _job;

    /// <summary>Creates the job; on Windows only.</summary>
    public BddRunnerJob()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _job = CreateJobObject(IntPtr.Zero, null);
        if (_job.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var limits = new ExtendedLimits { Basic = new BasicLimits { LimitFlags = KillOnJobClose } };
        if (!SetInformationJobObject(_job, ExtendedLimitInformation, ref limits, Marshal.SizeOf<ExtendedLimits>()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    /// <summary>Puts a process, and what it starts from now on, in the job. False when it has ended already.</summary>
    /// <param name="process">The process.</param>
    public bool Add(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (_job is null)
        {
            return true;
        }

        try
        {
            return AssignProcessToJobObject(_job, process.Handle);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Closes the job, which ends every process in it.</summary>
    public void Dispose() => _job?.Dispose();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int infoClass, ref ExtendedLimits info, int length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
