
namespace Eokas
{
    internal class Program
    {
        private static string EOKAS_VERSION = "0.0.1";

        static int Main(string[] args)
        {
            Command program = new Command(args[0]);
            program.Action((cmd) => { About(); });
            program.SubCommand("help", "")
                .Action((cmd) => { Help(); });

            program.SubCommand("compile", "")
                .Option("--file,-f", "", "")
                .Action((cmd) =>
                {
                    var file = cmd.FetchValue("--file");
                    if (file == "")
                        throw new Exception("The argument 'file' is empty.");

                    Console.WriteLine("=> Source file: {0}", file);

                    EokasMain(file, LLVMEngine.AOT);
                });

            program.SubCommand("run", "")
                .Option("--file,-f", "", "")
                .Action((cmd) =>
                {
                    var file = cmd.FetchValue("--file");
                    if (file == "")
                        throw new Exception("The argument 'file' is empty.");

                    Console.WriteLine("=> Source file: {0}", file);

                    EokasMain(file, LLVMEngine.JIT);
                });

            try
            {
                program.Execute(args);
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: {0}", e.Message);
                return -1;
            }
        }

        static void EokasMain(string fileName, Predicate<AstNodeModule> method)
        {
            var reader = File.OpenText(fileName);
            var source = reader.ReadToEnd();
            reader.Close();

            Console.WriteLine("=> Source code:");
            Console.WriteLine("------------------------------------------");
            Console.WriteLine("{0}", source.Replace("%", "%%"));
            Console.WriteLine("------------------------------------------");

            Parser parser = new Parser();
            AstNodeModule m = parser.Parse(source);
            if (m == null)
            {
                Console.WriteLine("ERROR: {0}", parser.error);
                return;
            }

            var writer = File.OpenWrite(string.Format("{0}.ll", fileName));
            Console.WriteLine("=> Encode to IR:");
            Console.WriteLine("------------------------------------------");
            method(m);
            Console.WriteLine("------------------------------------------");
            writer.Flush();
            writer.Close();
        }

        static void About()
        {
            Console.WriteLine("eokas {0}\n", EOKAS_VERSION);
        }

        static void Help()
        {
            Console.Write(@"
-?, -help
\tPrint command line help message.
fileName [-c] [-e] [-t]
\tComple or Execute a file, show exec-time.
"
            );
        }
    }
}