using System.Runtime.InteropServices;

namespace Borderless.App.Services;

/// <summary>
/// Mutes Core Audio sessions for a process via WASAPI session APIs.
/// </summary>
public sealed class AudioMuteService : IDisposable
{
    private static readonly Guid AudioSessionManager2Iid = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    private static readonly Guid MmDeviceEnumeratorClsid = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    private readonly Dictionary<int, bool> _desiredMute = new();
    private readonly Dictionary<int, bool> _appliedMute = new();
    private readonly object _gate = new();
    private bool _disposed;

    public void SetMuteDesired(int processId, bool mute)
    {
        lock (_gate)
        {
            if (_desiredMute.TryGetValue(processId, out var desired)
                && desired == mute
                && _appliedMute.TryGetValue(processId, out var applied)
                && applied == mute)
            {
                return;
            }

            _desiredMute[processId] = mute;
        }

        // Mark applied even if no session yet; Refresh re-asserts for late sessions.
        TryApplyMute(processId, mute);
        lock (_gate)
        {
            _appliedMute[processId] = mute;
        }
    }

    public void Clear(int processId)
    {
        lock (_gate)
        {
            _desiredMute.Remove(processId);
            _appliedMute.Remove(processId);
        }

        TryApplyMute(processId, false);
    }

    public void Refresh()
    {
        Dictionary<int, bool> snapshot;
        lock (_gate)
        {
            snapshot = new Dictionary<int, bool>(_desiredMute);
        }

        foreach (var (processId, mute) in snapshot)
        {
            // Re-assert so newly created audio sessions pick up the desired mute.
            if (TryApplyMute(processId, mute))
            {
                lock (_gate)
                {
                    _appliedMute[processId] = mute;
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Dictionary<int, bool> snapshot;
        lock (_gate)
        {
            snapshot = new Dictionary<int, bool>(_desiredMute);
            _desiredMute.Clear();
            _appliedMute.Clear();
        }

        foreach (var processId in snapshot.Keys)
        {
            TryApplyMute(processId, false);
        }
    }

    private static bool TryApplyMute(int processId, bool mute)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioSessionManager2? sessionManager = null;
        IAudioSessionEnumerator? sessionEnumerator = null;
        var applied = false;

        try
        {
            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(MmDeviceEnumeratorClsid)!)!;
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out device) != 0 || device is null)
            {
                return false;
            }

            var iid = AudioSessionManager2Iid;
            if (device.Activate(ref iid, ClsCtx.InProcServer, IntPtr.Zero, out var managerObj) != 0)
            {
                return false;
            }

            sessionManager = (IAudioSessionManager2)managerObj;
            if (sessionManager.GetSessionEnumerator(out sessionEnumerator) != 0 || sessionEnumerator is null)
            {
                return false;
            }

            sessionEnumerator.GetCount(out var count);
            for (var i = 0; i < count; i++)
            {
                if (sessionEnumerator.GetSession(i, out var sessionControl) != 0 || sessionControl is null)
                {
                    continue;
                }

                try
                {
                    if (sessionControl is not IAudioSessionControl2 sessionControl2)
                    {
                        continue;
                    }

                    if (sessionControl2.GetProcessId(out var sessionPid) != 0 || (int)sessionPid != processId)
                    {
                        continue;
                    }

                    if (sessionControl is ISimpleAudioVolume volume)
                    {
                        var eventContext = Guid.Empty;
                        if (volume.SetMute(mute, ref eventContext) == 0)
                        {
                            applied = true;
                        }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(sessionControl);
                }
            }
        }
        catch
        {
            // Audio sessions come and go; ignore transient COM failures.
        }
        finally
        {
            if (sessionEnumerator is not null)
            {
                Marshal.ReleaseComObject(sessionEnumerator);
            }

            if (sessionManager is not null)
            {
                Marshal.ReleaseComObject(sessionManager);
            }

            if (device is not null)
            {
                Marshal.ReleaseComObject(device);
            }

            if (enumerator is not null)
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }

        return applied;
    }

    private enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2
    }

    private enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    [Flags]
    private enum ClsCtx
    {
        InProcServer = 0x1
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void NotImpl1();

        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, ClsCtx dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        void NotImpl1();
        void NotImpl2();

        [PreserveSig]
        int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig]
        int GetCount(out int sessionCount);

        [PreserveSig]
        int GetSession(int sessionCount, out IAudioSessionControl2 session);
    }

    [ComImport]
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        // IAudioSessionControl (9 methods)
        void GetState(out int pRetVal);
        void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);
        void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);
        void GetGroupingParam(out Guid pRetVal);
        void SetGroupingParam(ref Guid overrideParam, ref Guid eventContext);
        void RegisterAudioSessionNotification(IntPtr newNotifications);
        void UnregisterAudioSessionNotification(IntPtr newNotifications);

        // IAudioSessionControl2
        [PreserveSig]
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

        [PreserveSig]
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

        [PreserveSig]
        int GetProcessId(out uint pRetVal);

        [PreserveSig]
        int IsSystemSoundsSession();

        [PreserveSig]
        int SetDuckingPreference(bool optOut);
    }

    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig]
        int SetMasterVolume(float fLevel, ref Guid eventContext);

        [PreserveSig]
        int GetMasterVolume(out float pfLevel);

        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid eventContext);

        [PreserveSig]
        int GetMute(out bool pbMute);
    }
}
