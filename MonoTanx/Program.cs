
MonoTanx.GameOptions options;
try
{
    options = MonoTanx.GameOptions.Parse(args);
}
catch (System.ArgumentException exception)
{
    System.Console.Error.WriteLine(exception.Message);
    System.Console.Error.WriteLine("Usage: MonoTanx [--seed <integer>]");
    return 1;
}

using var game = new MonoTanx.Tanx(options);
game.Run();
return 0;
