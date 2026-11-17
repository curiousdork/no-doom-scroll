using Cocona;

var app = CoconaApp.Create();

app.AddCommand("get", ([Argument] string key) =>
{
    Console.WriteLine($"get {key}");
}).WithDescription("Get the value for a key.");

app.AddCommand("set", ([Argument] string key, [Argument] string value) =>
{
    Console.WriteLine($"set {key} {value}");
}).WithDescription("Set the value for a key.");

app.AddCommand("rm", ([Argument] string key) =>
{
    Console.WriteLine($"rm {key}");
}).WithDescription("Delete a key.");

app.AddCommand("ls", () => {
    Console.WriteLine("Prints out all keys");
}).WithDescription("Prints all keys");

app.Run();
