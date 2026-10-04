Run with .NET 8:

  dotnet run --project tests/ReportingCollectorTests -p:RuntimeDefine=MONO -p:SideDefine=CLIENT

Repeat for RuntimeDefine=IL2CPP and SideDefine=SERVER (four combinations).
This test uses Lib.Harmony 2.4.2 only in the test process; it does not replace the
Harmony version shipped by MelonLoader or add a mod runtime dependency.

The managed fake uses the current game's exact reporting collector type and
method names. Its unpatched callback throws, then the production Harmony patch
is applied through Harmony's real class processor. Checks cover correct target
resolution, dedicated-session bypass, other log listeners remaining active,
ordinary client sessions retaining the native callback, and unpatch behavior.
Before adding the guard the test failed because the current reporting type
had no dedicated-session patch.

This reproduces callback interception and exception isolation, not native
IL2CPP physics, customer hand-ins, or actual map UI updates. Verify those in
an isolated multiplayer session before claiming customer progression is fixed.
