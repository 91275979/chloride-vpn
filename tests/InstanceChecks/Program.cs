using System.Diagnostics;
using System.Runtime.ExceptionServices;
using ServiceLib.Helper;

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("These real kernel-object checks require Windows.");
var count = 0;
void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
    count++;
}
T Worker<T>(Func<T> action)
{
    T result = default!;
    Exception? error = null;
    var thread = new Thread(() => { try { result = action(); } catch (Exception ex) { error = ex; } });
    thread.Start();
    if (!thread.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("Worker did not finish.");
    if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    return result;
}
var exe = @"C:\InstanceChecks\" + Guid.NewGuid().ToString("N") + @"\v2rayN.exe";
const string sid = "S-1-5-21-1001";
var key = PersonalInstanceCoordinator.GetIdentity(exe, sid);
Check(key.StartsWith("Local\\ChlorideVPN_", StringComparison.Ordinal), "Identity must be session-local.");
Check(key == PersonalInstanceCoordinator.GetIdentity(exe.ToLowerInvariant(), sid.ToLowerInvariant()), "Path/SID casing must not create an instance.");
Check(key == PersonalInstanceCoordinator.GetIdentity(exe.Replace(@"\v2rayN.exe", @"\sub\..\v2rayN.exe"), sid), "Full-path normalization.");
Check(key != PersonalInstanceCoordinator.GetIdentity(exe, "S-1-5-21-1002"), "User identities must be isolated.");
Check(key != PersonalInstanceCoordinator.GetIdentity(exe + ".other", sid), "Different executables must be isolated.");
Check(key.Length == "Local\\ChlorideVPN_".Length + 64, "Identity uses a SHA256 digest, not a raw path or SID.");
try { PersonalInstanceCoordinator.GetIdentity(exe, " "); throw new Exception("Accepted empty SID."); }
catch (ArgumentException) { count++; }
try { PersonalInstanceCoordinator.Acquire(exe, sid, TimeSpan.FromMilliseconds(-1)); throw new Exception("Accepted negative timeout."); }
catch (ArgumentOutOfRangeException) { count++; }
try { PersonalInstanceCoordinator.Acquire(exe, sid, TimeSpan.FromSeconds(11)); throw new Exception("Accepted unbounded timeout."); }
catch (ArgumentOutOfRangeException) { count++; }

var owner = PersonalInstanceCoordinator.Acquire(exe, sid);
Check(owner.IsOwner && owner.ShowRequested != null, "First caller must own the GUI and event.");
Check(!owner.ShowRequested!.WaitOne(0), "Fresh owner must have no phantom show request.");
var reopened = Worker(() =>
{
    using var second = PersonalInstanceCoordinator.Acquire(exe.ToLowerInvariant(), sid);
    return !second.IsOwner && second.ShowRequested == null;
});
Check(reopened, "Cross-thread reopening must forward without owning the GUI.");
Check(owner.ShowRequested.WaitOne(TimeSpan.FromSeconds(1)), "Owner must receive the forwarded show request.");
Check(!owner.ShowRequested.WaitOne(0), "The show request must auto-reset.");
var timed = Worker(() =>
{
    var timer = Stopwatch.StartNew();
    using var second = PersonalInstanceCoordinator.Acquire(exe, sid, TimeSpan.FromMilliseconds(180));
    return (!second.IsOwner, timer.Elapsed);
});
Check(timed.Item1, "Handoff timeout must not bypass ownership.");
Check(timed.Elapsed >= TimeSpan.FromMilliseconds(140) && timed.Elapsed < TimeSpan.FromSeconds(2), "Handoff wait must be bounded and observed.");
Check(owner.ShowRequested.WaitOne(TimeSpan.FromSeconds(1)), "Timed-out handoff must show the existing owner.");
var wrongThreadProtected = Worker(() =>
{
    try { owner.Dispose(); return false; }
    catch (InvalidOperationException) { return true; }
});
Check(wrongThreadProtected, "Wrong-thread disposal must not release a mutex it does not own.");
owner.Dispose();
owner.Dispose();
Check(owner.ShowRequested == null, "Disposed owner must not expose its event.");
var released = Worker(() =>
{
    using var next = PersonalInstanceCoordinator.Acquire(exe, sid);
    return next.IsOwner;
});
Check(released, "Disposal must allow a new owner.");

owner = PersonalInstanceCoordinator.Acquire(exe, sid);
using var waiting = new ManualResetEventSlim();
Exception? handoffError = null;
var handoffOwned = false;
var handoff = new Thread(() =>
{
    try
    {
        waiting.Set();
        using var next = PersonalInstanceCoordinator.Acquire(exe, sid, TimeSpan.FromSeconds(2));
        handoffOwned = next.IsOwner;
    }
    catch (Exception ex) { handoffError = ex; }
});
handoff.Start();
Check(waiting.Wait(TimeSpan.FromSeconds(1)), "Handoff worker started.");
Thread.Sleep(70);
owner.Dispose();
Check(handoff.Join(TimeSpan.FromSeconds(3)), "Handoff worker finished within bound.");
if (handoffError != null) ExceptionDispatchInfo.Capture(handoffError).Throw();
Check(handoffOwned, "Genuine handoff acquires only after the original owner releases.");
using (var independentUser = PersonalInstanceCoordinator.Acquire(exe, "S-1-5-21-1002"))
    Check(independentUser.IsOwner, "A different per-user guard can own independently.");
Console.WriteLine($"PASS: {count} instance identity, forwarding, timeout, release and handoff checks.");
