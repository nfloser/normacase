namespace NormaCase.Cli;

public static class Program
{
    public static Task<int> Main(string[] args)
        => CommandRunner.RunAsync(args, Console.Out, Console.Error);
}
