using DedicatedServerMod.Server.Commands.BuiltIn.System;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Commands.Output;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Permissions;
using DedicatedServerMod.Shared.ConsoleSupport;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Shared.Networking.Messaging;
using DedicatedServerMod.Shared.Permissions;
using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Client.Core;
using DedicatedServerMod.Utils;
using Newtonsoft.Json;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;

int checks = 0;
PlayerManager players = new();
ConnectedPlayerInfo healthy = new(); players.Players.Add(healthy);
players.Players.Add(new() { IsLoopbackConnection = true });
players.Players.Add(new() { IsAuthenticated = false });
players.Players.Add(new() { Connection = null });
players.Players.Add(new() { Connection = new() { IsActive = false } });
players.Players.Add(new() { Connection = new() { Reject = true } });
BroadcastCommand command = new(players);
Output output = new();
CommandContext context = new() { Arguments = new(), Output = output };
foreach (string invalid in new[] { "", "  ", new string('a',241), "warning\nshutdown", "warning\0", "warning\ttext" })
{
    context.Arguments = new() { invalid }; command.Execute(context);
    Require(MessagingService.Sent.Count == 0, "Invalid announcement was sent.");
}
Require(output.Errors.Count == 6, "Invalid input needs a console error.");
context.Arguments = new() { "Server restarting in 5 minutes" };
MessagingService.IsEndpointReady = false; command.Execute(context);
Require(MessagingService.Sent.Count == 0 && output.Errors.Last().Contains("not ready"), "An unavailable endpoint accepted a stale warning.");
MessagingService.IsEndpointReady = true;
var parsed = CommandLineParser.TryParse("broadcast \"Server restarting in 5 minutes — don't lose progress.\"");
Require(parsed.Success, "Quoted schedule payload failed parsing.");
context.Arguments = parsed.CommandLine.Arguments.ToList(); command.Execute(context);
Require(MessagingService.Sent.Count == 1 && MessagingService.Sent[0].Connection == healthy.Connection, "Broadcast reached a ghost, unauthenticated, disconnected or failed recipient.");
Require(MessagingService.Sent[0].Command == Constants.Messages.ServerAnnouncement, "Wrong announcement route.");
Require(output.Info.Last().Contains("1 player(s)") && output.Warnings.Single().Contains("1 player(s)"), "Console counts must reflect send acceptance.");
var decoded = JsonConvert.DeserializeObject<ServerAnnouncementMessage>(MessagingService.Sent[0].Payload);
Require(decoded.Message == "Server restarting in 5 minutes — don't lose progress.", "Message was changed by parsing/serialization.");
var notification = new NotificationsManager(); Singleton<NotificationsManager>.Instance = notification;
ClientAnnouncementHandler.Handle(MessagingService.Sent[0].Payload);
Require(notification.Shown.Single().Message == decoded.Message && notification.Shown[0].Duration == 10f && notification.Shown[0].Sound, "Announcement did not reach the game's visible notification API.");
foreach (string invalid in new[] { "null", "{", "{}", "{\"Message\":42}", JsonConvert.SerializeObject(new { Message = new string('x',241) }), new string('x',4097) })
{
    int before = notification.Shown.Count; ClientAnnouncementHandler.Handle(invalid);
    // Json.NET accepts numeric text; it remains bounded and safe to display.
    if (invalid.Contains("42")) Require(notification.Shown.Last().Message == "42", "Convertible JSON text is not handled consistently.");
    else Require(notification.Shown.Count == before, "Invalid JSON reached the UI.");
}
int shown = notification.Shown.Count;
ClientBootstrap.Instance.ConnectionManager.IsConnectedToDedicatedServer = false;
ClientAnnouncementHandler.Handle(MessagingService.Sent[0].Payload);
Require(notification.Shown.Count == shown, "Announcement appeared outside a dedicated session.");
ClientBootstrap.Instance.ConnectionManager.IsConnectedToDedicatedServer = true;
Singleton<NotificationsManager>.Instance = null; ClientAnnouncementHandler.Handle(MessagingService.Sent[0].Payload);
Singleton<NotificationsManager>.Instance = notification; notification.Throw = true;
ClientAnnouncementHandler.Handle(MessagingService.Sent[0].Payload);
Require(notification.Shown.Count == shown, "UI teardown handling is incorrect.");
var store = PermissionDefaults.CreateSeedData("test");
Require(store.Groups[PermissionBuiltIns.Groups.Administrator].Allow.Contains(command.RequiredPermissionNode), "Administrator is missing broadcast permission.");
Require(!store.Groups[PermissionBuiltIns.Groups.Default].Allow.Contains(command.RequiredPermissionNode), "Ordinary players received broadcast permission.");
Require(command.RequiredPermissionNode == "server.broadcast" && PermissionDefaults.GetBuiltInDefinitions().Any(x => x.Node == command.RequiredPermissionNode), "Permission metadata is missing.");
MessagingService.Sent.Clear(); players.Players.Clear(); command.Execute(context);
Require(output.Info.Last().Contains("0 player(s)"), "Empty server needs an honest no-recipient reply.");
Console.WriteLine($"PASS|ServerAnnouncementTests|checks={checks}");
void Require(bool value,string message) { if (!value) throw new Exception(message); checks++; }
sealed class Output : ICommandOutput
{
    public List<string> Info { get; } = new(); public List<string> Warnings { get; } = new(); public List<string> Errors { get; } = new();
    public void WriteInfo(string value) => Info.Add(value);
    public void WriteWarning(string value) => Warnings.Add(value);
    public void WriteError(string value) => Errors.Add(value);
}
