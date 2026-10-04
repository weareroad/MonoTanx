
MonoTanx.GameOptions options;
try
{
    options = MonoTanx.GameOptions.Parse(args);
}
catch (System.ArgumentException exception)
{
    System.Console.Error.WriteLine(exception.Message);
    System.Console.Error.WriteLine("Usage: MonoTanx [--test] [--two-player | --demo] [--mute] [--seed <integer>] [--windowed [--scale <1-4>]]");
    return 1;
}

using var game = new MonoTanx.Tanx(options);
game.Run();
return 0;
