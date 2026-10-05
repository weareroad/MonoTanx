MonoTanx.GameOptions options;
try
{
    options = MonoTanx.GameOptions.Parse(args);
}
catch (System.ArgumentException exception)
{
    System.Console.Error.WriteLine(exception.Message);
    System.Console.Error.WriteLine(MonoTanx.GameOptions.UsageLine);
    System.Console.Error.WriteLine("Run MonoTanx --help for what each option does.");
    return 1;
}

if (options.ShowHelp)
{
    System.Console.WriteLine(MonoTanx.GameOptions.HelpText);
    return 0;
}

using var game = new MonoTanx.Tanx(options);
game.Run();
return 0;
