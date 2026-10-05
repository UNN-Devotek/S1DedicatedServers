using DedicatedServerMod.Server.Commands.BuiltIn.Gameplay;
using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Commands.Output;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Permissions;
using DedicatedServerMod.Shared.ConsoleSupport;
using DedicatedServerMod.Shared.Networking.Messaging;
using DedicatedServerMod.Shared.Permissions;
#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Vehicles;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.Vehicles;
#endif

int checks = 0;
PlayerManager players = new();
ConnectedPlayerInfo dev = new() { PlayerName = "Devotek", SteamId = "76561190000000001", AuthenticatedSteamId = "76561190000000002", ClientId = 5 };
ConnectedPlayerInfo other = new() { PlayerName = "Dev Friend", SteamId = "76561190000000003", ClientId = 6 };
players.Players.AddRange(new[] { dev, other });
SpawnVehicleCommand spawn = new(players);
GiveCommand give = new(players);
VehicleManager vehicles = new();
NetworkSingleton<VehicleManager>.Instance = vehicles;
Output output = new();

Run(spawn, "spawnvehicle Devotek SHITBOX");
Require(vehicles.Spawned.Single().Owned, "Vehicle must be player owned.");
var position = vehicles.Spawned.Single().Position;
Require(position.x == 10 && position.y == 21 && position.z == 34, "Vehicle must spawn ahead of the target.");
Require(output.Info.Single().Contains("Devotek"), "Spawn result should identify the target.");
Run(spawn, "spawnvehicle shitbox", dev);
Require(vehicles.Spawned.Count == 1, "In-game self spawn must keep working.");
foreach (string id in new[] { "5", dev.SteamId, dev.AuthenticatedSteamId, "Devotek", "vote" })
{
    Run(give, $"give {id} baggie 5");
    Require(MessagingService.Sent.Single().Connection == dev.Connection, "Target ID resolved the wrong connection: " + id);
}
Run(give, "give \"Dev Friend\" baggie");
Require(MessagingService.Sent.Single().Connection == other.Connection && MessagingService.Sent.Single().Payload == "give baggie 1", "Quoted name/default quantity failed.");
Run(give, "give BAGGIE 7", dev);
Require(MessagingService.Sent.Single().Payload == "give baggie 7", "In-game self grant must preserve vanilla syntax.");
Run(give, "give baggie", dev);
Require(MessagingService.Sent.Single().Payload == "give baggie 1", "In-game default quantity failed.");
Run(give, "give \"Dev Friend\" baggie 2", dev);
Require(MessagingService.Sent.Single().Connection == other.Connection, "In-game explicit target failed.");
Require(MessagingService.Sent.Single().Command == "exec_console" && output.Info.Single().Contains("not confirmed"), "Grant must use the existing client relay without claiming delivery.");
Run(give, "give Devotek baggie 1000");
Require(MessagingService.Sent.Count == 1, "Maximum permitted quantity was rejected.");

foreach (string line in new[] { "spawnvehicle", "spawnvehicle shitbox", "spawnvehicle Devotek shitbox extra", "spawnvehicle Missing shitbox", "spawnvehicle Dev shitbox", "spawnvehicle Devotek invalid", "spawnvehicle \"\" shitbox" })
    Reject(spawn, line);
foreach (string line in new[] { "give", "give baggie", "give Devotek baggie 1 extra", "give Missing baggie", "give Dev baggie", "give Devotek unknown", "give \"\" baggie" })
    Reject(give, line);
foreach (string quantity in new[] { "0", "-1", "1001", "2147483648", "1.5", "many", "NaN", "\"\"", "+1", "1e2" })
    Reject(give, $"give Devotek baggie {quantity}");

players.Players.Add(new() { PlayerName = "Devotek", ClientId = 20 });
Reject(spawn, "spawnvehicle Devotek shitbox");
players.Players.RemoveAt(players.Players.Count - 1);
dev.IsAuthenticated = false;
Reject(give, "give Devotek baggie");
dev.IsAuthenticated = true;
dev.IsLoopbackConnection = true;
Reject(spawn, "spawnvehicle Devotek shitbox");
Reject(give, "give baggie", dev);
dev.IsLoopbackConnection = false;
dev.Connection.IsActive = false;
Reject(spawn, "spawnvehicle Devotek shitbox");
dev.Connection.IsActive = true;
dev.IsDisconnectProcessed = true;
Reject(give, "give Devotek baggie");
dev.IsDisconnectProcessed = false;
var player = dev.PlayerInstance;
dev.PlayerInstance = null;
Reject(spawn, "spawnvehicle Devotek shitbox");
dev.PlayerInstance = player;
MessagingService.IsEndpointReady = false;
Reject(give, "give Devotek baggie");
MessagingService.IsEndpointReady = true;
dev.Connection.Reject = true;
Reject(give, "give Devotek baggie");
dev.Connection.Reject = false;
NetworkSingleton<VehicleManager>.Instance = null;
Reject(spawn, "spawnvehicle Devotek shitbox");
NetworkSingleton<VehicleManager>.Instance = vehicles;
vehicles.Fail = true;
Reject(spawn, "spawnvehicle Devotek shitbox");
vehicles.Fail = false;
foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    dev.PlayerInstance.transform.position.x = invalid;
    Reject(spawn, "spawnvehicle Devotek shitbox");
}
dev.PlayerInstance.transform.position.x = 10;
dev.PlayerInstance.transform.rotation.w = float.NaN;
Reject(spawn, "spawnvehicle Devotek shitbox");
dev.PlayerInstance.transform.rotation.w = 0;
Reject(spawn, "spawnvehicle Devotek shitbox");
dev.PlayerInstance.transform.rotation.w = 1;

Require(spawn.RequiredPermissionNode == "console.command.spawnvehicle" && give.RequiredPermissionNode == "console.command.give", "Commands must preserve their existing permission nodes.");
var groups = PermissionDefaults.CreateSeedData("test").Groups;
Require(groups[PermissionBuiltIns.Groups.Operator].Allow.Contains(PermissionBuiltIns.Nodes.ConsoleCommandWildcard), "Operators need gameplay command authorization.");
Require(!groups[PermissionBuiltIns.Groups.Default].Allow.Contains(PermissionBuiltIns.Nodes.ConsoleCommandWildcard), "Normal players must not receive gameplay command authorization.");
Console.WriteLine($"Passed {checks} gameplay command checks.");

void Run(IServerCommand command, string line, ConnectedPlayerInfo executor = null)
{
    output.Info.Clear(); output.Errors.Clear();
    vehicles.Spawned.Clear(); MessagingService.Sent.Clear();
    var parsed = CommandLineParser.TryParse(line);
    if (!parsed.Success) throw new Exception("Test command failed to parse: " + line);
    command.Execute(new CommandContext { Arguments = parsed.CommandLine.Arguments.ToList(), Executor = executor, PlayerManager = players, Output = output });
}
void Reject(IServerCommand command, string line, ConnectedPlayerInfo executor = null)
{
    Run(command, line, executor);
    Require(output.Errors.Count > 0 && output.Info.Count == 0 && vehicles.Spawned.Count == 0 && MessagingService.Sent.Count == 0,
        "Invalid command must fail without mutation: " + line);
}
void Require(bool value, string message)
{
    if (!value) throw new Exception(message);
    checks++;
}
sealed class Output : ICommandOutput
{
    public List<string> Info = new(), Errors = new();
    public void WriteInfo(string message) => Info.Add(message);
    public void WriteWarning(string message) { }
    public void WriteError(string message) => Errors.Add(message);
}
