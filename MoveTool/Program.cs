using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MoveLib;
using MoveLib.BAC;
using MoveLib.BCM;

namespace MoveTool
{
    public class Program
    {
        private static readonly char Separator = Path.DirectorySeparatorChar;

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;

            Start(args);
        }

        private static void Start(string[] args)
        {
            BAC.StaleTypeCountDecision = AskAboutStaleTypeCounts;

            switch (args.Length)
            {
                case 0:
                {
                    Console.WriteLine("\nBAC/BCM/BCH to JSON: MoveTool.exe InFile.uasset OutFile.json"      + 
                                      "\nJSON to BAC/BCM/BCH: MoveTool.exe InFile.json OutFile.uasset"      + 
                                      "\n\nYou can also drag and drop files onto this tool and it will"     + 
                                      "\nautomatically create the JSON or BAC/BCM/BCH file with the "       +
                                      "\nsame name in the same directory as the original file."             + 
                                      ("\n\nBack up your files, this tool will overwrite any file with the" + 
                                      "\nsame name as the output file!").ToUpper()                          );

                    break;
                }

                case 1:
                {
                    var path = args[0];
                    var directory = Path.GetDirectoryName(path) + Separator;
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
                    
                    Console.WriteLine(directory + fileNameWithoutExtension);

                    // Check if file exists
                    if (!File.Exists(path))
                    {
                        Console.WriteLine("File does not exist: " + path);
                        break;
                    }

                    #region Handle .UASSET files

                    if (path.ToLower().EndsWith("uasset"))
                    {
                        var type = FileTypeDecider.Decide(path);

                        switch (type)
                        {
                            case FileType.BAC:
                                Console.WriteLine("BAC file detected. Trying to do BAC to JSON.");
                                try
                                {
                                    BAC.BacToJson(path, directory + fileNameWithoutExtension + ".json");

                                    Console.WriteLine("Done writing file: " + 
                                                      directory + fileNameWithoutExtension + ".json");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Something went wrong: " + ex.Message + " - " + ex.Data);
                                }

                                break;

                            case FileType.BCM:
                                try
                                {
                                    Console.WriteLine("BCM file detected. Trying to do BCM to JSON.");

                                    BCM.BcmToJson(path,
                                        directory + fileNameWithoutExtension + ".json");

                                    Console.WriteLine("Done writing file: " + 
                                                      directory + fileNameWithoutExtension + ".json");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Something went wrong: " + ex.Message + " - " + ex.Data);
                                }

                                break;

                            case FileType.BCH:
                                try
                                {
                                    Console.WriteLine("BCH file detected. Trying to do BCH to JSON.");
                                    BCH.BchToJson(path, 
                                        directory + fileNameWithoutExtension + ".json");
                                    Console.WriteLine("Done writing file: " + 
                                                      directory + fileNameWithoutExtension + ".json");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Something went wrong: " + ex.Message + " - " + ex.Data);
                                }

                                break;

                            case FileType.Unknown:
                            default:
                                Console.WriteLine("Unsupported format.");
                                break;
                        }
                    }

                    #endregion

                    #region Handle .JSON files

                    if (args[0].ToLower().EndsWith("json"))
                    {
                        ConvertJsonToUasset(args[0], directory + fileNameWithoutExtension + ".uasset");
                    }

                    #endregion

                    break;
                }

                case 2:
                {
                    var inFile = args[0];
                    var outFile = args[1];

                    if (inFile.ToLower().EndsWith("uasset"))
                    {
                        if (!outFile.ToLower().EndsWith("json"))
                        {
                            outFile += ".json";
                        }

                        var type = FileTypeDecider.Decide(inFile);

                        switch (type)
                        {
                            case FileType.BAC:
                                Console.WriteLine("BAC file detected. Trying to do BAC to JSON.");
                                BAC.BacToJson(inFile, outFile);
                                Console.WriteLine("Done writing file: " + outFile);
                                break;

                            case FileType.BCM:
                                Console.WriteLine("BCM file detected. Trying to do BCM to JSON.");
                                BCM.BcmToJson(inFile, outFile);
                                Console.WriteLine("Done writing file: " + outFile);
                                break;

                            case FileType.BCH:
                                Console.WriteLine("BCH file detected. Trying to do BCH to JSON.");
                                BCH.BchToJson(inFile, outFile);
                                Console.WriteLine("Done writing file: " + outFile);
                                break;

                            case FileType.Unknown:
                            default:
                                Console.WriteLine("Unsupported format.");
                                break;
                        }
                    }
                    else if (inFile.ToLower().EndsWith("json"))
                    {
                        if (!outFile.ToLower().EndsWith("uasset"))
                        {
                            outFile += ".uasset";
                        }

                        ConvertJsonToUasset(inFile, outFile);
                    }

                    break;
                }

                default:
                {
                    Console.WriteLine("MoveTool can not understand more than 2 arguments. \n" +
                                      @"If the paths contain spaces, try wrapping the paths in double quotes ("").");
                    break;
                }
            }

            Pause();
        }

        /// <summary>
        /// Asked once per BAC write when moves declare more type blocks than the json holds.
        /// A stale count is usually left over from an older tool, but correcting it does
        /// change the file, so the choice is the user's. Only BAC headers work this way;
        /// a BCM's short CancelInts lists are always preserved, as the missing pairs cannot
        /// be recovered.
        /// </summary>
        private static bool AskAboutStaleTypeCounts(IList<string> moves)
        {
            const int ShowAtMost = 10;

            Console.WriteLine();
            Console.WriteLine(moves.Count + " move(s) declare more type blocks than this file holds:");

            for (int i = 0; i < moves.Count && i < ShowAtMost; i++)
            {
                Console.WriteLine("  " + moves[i]);
            }

            if (moves.Count > ShowAtMost)
            {
                Console.WriteLine("  ... and " + (moves.Count - ShowAtMost) + " more.");
            }

            Console.WriteLine("The surplus entries overlap the tick data that follows the type list,");
            Console.WriteLine("so they are not extra moves data. Correcting makes each header match the");
            Console.WriteLine("blocks actually written; leaving them keeps the file exactly as found.");
            Console.Write("Correct these headers? (y = correct, any other key = leave as is): ");

            bool correct;

            if (Console.IsInputRedirected)
            {
                // Scripted runs: honour a piped answer, and default to leaving the file
                // alone when there is nothing to read.
                string answer = Console.In.ReadLine();
                correct = answer != null && answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
                Console.WriteLine(answer == null ? "(no answer given)" : answer.Trim());
            }
            else
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                correct = key.KeyChar == 'y' || key.KeyChar == 'Y';
                Console.WriteLine(key.KeyChar);
            }

            Console.WriteLine(correct
                ? "Correcting the headers."
                : "Leaving the headers as they are.");
            Console.WriteLine();

            return correct;
        }

        /// <summary>
        /// Converts a json back to its uasset, choosing the converter up front from the
        /// json's own contents. Trying each converter in turn used to report the failure
        /// of the wrong ones as an error, which read like the conversion had failed even
        /// when the right converter went on to succeed.
        /// </summary>
        private static void ConvertJsonToUasset(string inFile, string outFile)
        {
            Console.WriteLine("File is json. Reading it to work out which format it holds.");

            var type = FileTypeDecider.DecideJson(inFile);
            bool success;

            switch (type)
            {
                case FileType.BAC:
                    Console.WriteLine("Detected a BAC json. Converting it to a BAC uasset.");
                    success = BAC.JsonToBac(inFile, outFile);
                    break;

                case FileType.BCM:
                    Console.WriteLine("Detected a BCM json. Converting it to a BCM uasset.");
                    success = BCM.JsonToBcm(inFile, outFile);
                    break;

                case FileType.BCH:
                    Console.WriteLine("Detected a BCH json. Converting it to a BCH uasset.");
                    success = BCH.JsonToBch(inFile, outFile);
                    break;

                default:
                    Console.WriteLine(
                        "Could not tell which format this json holds, so nothing was converted." +
                        "\nA BAC json starts with \"MoveLists\", a BCM json with \"Charges\", " +
                        "a BCH json with \"BCH\"." +
                        "\nIf the file is one of those, it is most likely truncated or not valid json.");
                    return;
            }

            if (success)
            {
                Console.WriteLine("Success. Wrote " + type + " uasset: " + outFile);
            }
            else
            {
                Console.WriteLine("Conversion to " + type + " failed, so " + outFile +
                                  " was not written. The reason is printed above.");
            }
        }

        private static void Pause()
        {
            // ReadKey throws InvalidOperationException when MoveTool is run from
            // a script with redirected stdin/stdout. Interactive drag-and-drop
            // runs should still pause so the user can read the result.
            if (Console.IsInputRedirected)
            {
                return;
            }

            Console.Write("\n\nPress any key to continue...");
            Console.ReadKey(true);
            Console.WriteLine("\n");
        }

        // Part of enabling a single .exe file
        private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var assemblyName = new AssemblyName(args.Name);

            var path = assemblyName.Name + ".dll";
            if (assemblyName.CultureInfo.Equals(System.Globalization.CultureInfo.InvariantCulture) == false)
            {
                path = $@"{assemblyName.CultureInfo}\{path}";
            }

            using (var stream = executingAssembly.GetManifestResourceStream(path))
            {
                if (stream == null)
                    return null;

                var assemblyRawBytes = new byte[stream.Length];
                stream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
                return Assembly.Load(assemblyRawBytes);
            }
        }
    }
}
