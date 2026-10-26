using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ServiceLib.Helper;

/// <summary>
/// Owns one Windows GUI for one user and executable path. A reopening process
/// only requests that the owner show its window; it never initializes a core.
/// No access-control or integrity-level settings are changed by this class.
/// </summary>
public sealed class PersonalInstanceCoordinator : IDisposable
{
    public static readonly TimeSpan MaximumHandoffWait = TimeSpan.FromSeconds(10);
    private readonly Mutex _mutex;
    private readonly int _ownerThreadId;
    private EventWaitHandle? _showRequested;
    private bool _disposed;

    private PersonalInstanceCoordinator(Mutex mutex, bool isOwner, EventWaitHandle? showRequested)
    {
        _mutex = mutex;
        IsOwner = isOwner;
        _ownerThreadId = Environment.CurrentManagedThreadId;
        _showRequested = showRequested;
    }

    public bool IsOwner { get; }

    /// <summary>The owner's auto-reset show event; null for a reopening process.</summary>
    public EventWaitHandle? ShowRequested => _showRequested;

    /// <summary>
    /// Generates a Windows per-user identity. Normalizes path spelling and
    /// casing; does not resolve junctions or other filesystem aliases.
    /// This method is pure and does not open any kernel objects.
    /// </summary>
    public static string GetIdentity(string executablePath, string userSid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(userSid);
        var path = Path.GetFullPath(executablePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        var sid = userSid.Trim().ToUpperInvariant();
        var identity = sid.Length + ":" + sid + path;
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return "Local\\ChlorideVPN_" + digest;
    }

    /// <summary>
    /// Tries ownership immediately, or waits up to <paramref name="handoffWait"/>
    /// for an intentional restart handoff. On timeout, signals the existing
    /// owner and returns IsOwner=false. Errors propagate so callers fail closed.
    /// The owning result must be disposed on this same thread.
    /// </summary>
    public static PersonalInstanceCoordinator Acquire(string executablePath, string userSid, TimeSpan handoffWait = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The instance coordinator requires Windows.");
        if (handoffWait < TimeSpan.Zero || handoffWait > MaximumHandoffWait)
            throw new ArgumentOutOfRangeException(nameof(handoffWait));
        var identity = GetIdentity(executablePath, userSid);
        var mutex = new Mutex(false, identity + "_owner");
        var owned = false;
        EventWaitHandle? show = null;
        try
        {
            try { owned = mutex.WaitOne(handoffWait); }
            catch (AbandonedMutexException) { owned = true; }
            if (owned)
            {
                show = new EventWaitHandle(false, EventResetMode.AutoReset, identity + "_show");
                return new(mutex, true, show);
            }

            SignalExistingOwner(identity + "_show");
            return new(mutex, false, null);
        }
        catch
        {
            show?.Dispose();
            if (owned) mutex.ReleaseMutex();
            mutex.Dispose();
            throw;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SignalExistingOwner(string eventName)
    {
        // The owner takes the mutex before creating its show event. Allow only
        // that short creation race; access-denied and other errors are not
        // converted into a second owner or an alternate instance identity.
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                using var show = EventWaitHandle.OpenExisting(eventName);
                show.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException) when (timer.Elapsed < TimeSpan.FromSeconds(1))
            {
                Thread.Sleep(10);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (IsOwner && Environment.CurrentManagedThreadId != _ownerThreadId)
            throw new InvalidOperationException("The instance owner must release ownership on its acquiring thread.");
        _showRequested?.Dispose();
        _showRequested = null;
        if (IsOwner) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _disposed = true;
    }
}
