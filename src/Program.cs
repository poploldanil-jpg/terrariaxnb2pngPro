// XNBExporterPro - Program.cs
// Entry point with both GUI and command-line support

using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace XNBExporterPro
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // If arguments provided, run in CLI mode
            if (args.Length > 0)
            {
                return RunCli(args);
            }

            // Otherwise, run GUI
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        static int RunCli(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║     XNB Exporter Pro v2.0  (CLI Mode)   ║");
            Console.WriteLine("║     XNB → PNG/BMP/TGA Converter         ║");
            Console.WriteLine("╚══════════════════════════════════════════╝");
            Console.WriteLine();

            // Parse args
            string outputDir = null;
            ImageFormat format = ImageFormat.PNG;
            bool recursive = false;
            bool overwrite = true;
            var inputPaths = new System.Collections.Generic.List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-o":
                    case "--output":
                        if (i + 1 < args.Length) outputDir = args[++i];
                        break;
                    case "-f":
                    case "--format":
                        if (i + 1 < args.Length)
                        {
                            switch (args[++i].ToUpper())
                            {
                                case "PNG": format = ImageFormat.PNG; break;
                                case "BMP": format = ImageFormat.BMP; break;
                                case "TGA": format = ImageFormat.TGA; break;
                                default:
                                    Console.WriteLine($"Unknown format: {args[i]}. Using PNG.");
                                    break;
                            }
                        }
                        break;
                    case "-r":
                    case "--recursive":
                        recursive = true;
                        break;
                    case "--no-overwrite":
                        overwrite = false;
                        break;
                    case "-h":
                    case "--help":
                        PrintHelp();
                        return 0;
                    default:
                        inputPaths.Add(args[i]);
                        break;
                }
            }

            if (inputPaths.Count == 0)
            {
                Console.WriteLine("Error: No input files or folders specified.");
                Console.WriteLine();
                PrintHelp();
                return 1;
            }

            // Collect all XNB files
            var files = new System.Collections.Generic.List<string>();
            foreach (var path in inputPaths)
            {
                if (Directory.Exists(path))
                {
                    var searchOpt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    files.AddRange(Directory.GetFiles(path, "*.xnb", searchOpt));
                }
                else if (File.Exists(path))
                {
                    files.Add(path);
                }
                else
                {
                    Console.WriteLine($"Warning: Path not found: {path}");
                }
            }

            if (files.Count == 0)
            {
                Console.WriteLine("No XNB files found.");
                return 1;
            }

            Console.WriteLine($"Found {files.Count} XNB file(s)");
            Console.WriteLine($"Output format: {format}");
            if (outputDir != null)
                Console.WriteLine($"Output folder: {outputDir}");
            Console.WriteLine();

            string ext = ImageWriter.GetExtension(format);
            int success = 0, failed = 0, skipped = 0;

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                try
                {
                    string outDir = outputDir ?? Path.GetDirectoryName(file);
                    string outFile = Path.Combine(outDir,
                        Path.GetFileNameWithoutExtension(file) + ext);

                    if (!overwrite && File.Exists(outFile))
                    {
                        Console.WriteLine($"  SKIP  {fileName} (exists)");
                        skipped++;
                        continue;
                    }

                    if (!Directory.Exists(outDir))
                        Directory.CreateDirectory(outDir);

                    var texture = XnbReader.ReadTexture(file);
                    ImageWriter.Save(outFile, texture.Width, texture.Height,
                        texture.PixelData, format);

                    Console.WriteLine($"  OK    {fileName} → {texture.Width}×{texture.Height} {format}");
                    success++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  FAIL  {fileName}: {ex.Message}");
                    failed++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Results: {success} converted, {failed} failed, {skipped} skipped");
            return failed > 0 ? 1 : 0;
        }

        static void PrintHelp()
        {
            Console.WriteLine("Usage: XNBExporterPro.exe [options] <files/folders...>");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -f, --format <PNG|BMP|TGA>  Output format (default: PNG)");
            Console.WriteLine("  -o, --output <folder>       Output folder (default: same as input)");
            Console.WriteLine("  -r, --recursive             Search folders recursively");
            Console.WriteLine("  --no-overwrite              Skip existing files");
            Console.WriteLine("  -h, --help                  Show this help");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  XNBExporterPro.exe texture.xnb");
            Console.WriteLine("  XNBExporterPro.exe -f PNG -r ./Content/");
            Console.WriteLine("  XNBExporterPro.exe -o ./output/ *.xnb");
            Console.WriteLine();
            Console.WriteLine("Run without arguments to start GUI mode.");
        }
    }
}
