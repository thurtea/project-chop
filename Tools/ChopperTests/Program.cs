using System.Diagnostics;

internal static class Program
{
    private static string? FindRepo()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Tools", "playtest.ps1"))) return dir.FullName;
                dir = dir.Parent;
            }
        }
        return null;
    }

    private static bool EditorOpen() =>
        Process.GetProcessesByName("Unity").Any(p =>
        {
            try { return p.WorkingSet64 > 300L * 1024 * 1024; }
            catch { return false; }
        });

    private static int Run(string repo, string file, params string[] args)
    {
        var info = new ProcessStartInfo("pwsh") { WorkingDirectory = repo, UseShellExecute = false };
        info.ArgumentList.Add("-ExecutionPolicy");
        info.ArgumentList.Add("Bypass");
        info.ArgumentList.Add("-File");
        info.ArgumentList.Add(Path.Combine(repo, "Tools", file));
        foreach (string a in args) info.ArgumentList.Add(a);
        using Process? p = Process.Start(info);
        if (p == null) return -1;
        p.WaitForExit();
        return p.ExitCode;
    }

    private static void Open(string path)
    {
        if (File.Exists(path) || Directory.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        else Console.WriteLine($"Not found: {path}");
    }

    private static void Main()
    {
        string[] tours =
        {
            "Arrival", "Swim", "DoubleJump", "Combo", "Combat", "GuardTest", "Armour", "MoveTest", "BlockGround",
            "House", "Mine", "Crypt", "Fishing", "Concept", "GearLook", "Logging"
        };

        string? repo = FindRepo();
        if (repo == null)
        {
            Console.WriteLine("Could not find the Chopper repo (Tools\\playtest.ps1). Put ChopperTests.exe inside E:\\Projects\\project-chop.");
            if (!Console.IsInputRedirected) Console.ReadKey();
            return;
        }

        while (true)
        {
            if (!Console.IsOutputRedirected) Console.Clear();
            Console.WriteLine("Chopper tests");
            Console.WriteLine($"Repo: {repo}");
            if (EditorOpen()) Console.WriteLine("\n  The Unity editor is open. Close it before options 1-5 (batch jobs need the project).");
            Console.WriteLine();
            Console.WriteLine("  1  Everything: art checks, Unity checks, unit tests, build, title and every tour (about 20 min)");
            Console.WriteLine("  2  Quick: skip the art checks and unit tests");
            Console.WriteLine("  3  Tours only, on the current build");
            Console.WriteLine("  4  One tour");
            Console.WriteLine("  5  Build the game only");
            Console.WriteLine("  6  Play the game (Builds\\Dev\\Chopper.exe)");
            Console.WriteLine("  7  Open the report card");
            Console.WriteLine("  8  Open the playtest checklist");
            Console.WriteLine("  9  Open the latest results folder");
            Console.WriteLine("  0  Quit");
            Console.Write("\nChoose: ");
            string choice = Console.ReadLine()?.Trim() ?? "";
            Console.WriteLine();
            switch (choice)
            {
                case "1": Run(repo, "playtest.ps1"); break;
                case "2": Run(repo, "playtest.ps1", "-Quick"); break;
                case "3": Run(repo, "playtest.ps1", "-ToursOnly"); break;
                case "4":
                    for (int i = 0; i < tours.Length; i++) Console.WriteLine($"  {i + 1,2}  {tours[i]}");
                    Console.Write("\nTour number: ");
                    if (int.TryParse(Console.ReadLine(), out int n) && n >= 1 && n <= tours.Length)
                    {
                        Run(repo, "homework.ps1", "tour", tours[n - 1]);
                        string logs = Path.Combine(repo, "Logs", "homework");
                        string? shots = Directory.Exists(logs)
                            ? Directory.GetDirectories(logs, $"tour-{tours[n - 1]}-*").OrderByDescending(d => d).FirstOrDefault()
                            : null;
                        if (shots != null) Open(shots);
                    }
                    break;
                case "5": Run(repo, "homework.ps1", "build"); break;
                case "6": Open(Path.Combine(repo, "Builds", "Dev", "Chopper.exe")); continue;
                case "7": Open(Path.Combine(repo, "Chopper", "Docs", "Reportcard.md")); continue;
                case "8": Open(Path.Combine(repo, "Chopper", "Docs", "playtest-windows.md")); continue;
                case "9":
                    string folder = Path.Combine(repo, "Logs", "homework");
                    string? latest = Directory.Exists(folder)
                        ? Directory.GetDirectories(folder, "playtest-*").OrderByDescending(d => d).FirstOrDefault()
                        : null;
                    Open(latest ?? folder);
                    continue;
                case "0": return;
                default: continue;
            }
            Console.WriteLine("\nDone. Press a key for the menu.");
            if (!Console.IsInputRedirected) Console.ReadKey();
        }
    }
}
