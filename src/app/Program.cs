
namespace Eokas
{
    using LLVMSharp;
    
    internal class Program
    {
        private static string EOKAS_VERSION = "0.0.1";
        
        static int Main(string[] args)
        {
            Command program = new Command(args[0]);
            program.Action((cmd) =>
            {
                About();
            });
            program.SubCommand("help", "")
                .Action((cmd)=>
                {
                    Help();
                });
	
            program.SubCommand("compile", "")
                .Option("--file,-f", "", "")
                .Action((cmd)=>
                {
                    var file = cmd.FetchValue("--file");
                    if(file == "")
                        throw new Exception("The argument 'file' is empty.");
                    
                    Console.WriteLine("=> Source file: {0}", file);
			
                    //eokas_main(file, llvm_aot);
                });

            program.SubCommand("run", "")
                .Option("--file,-f", "", "")
                .Action((cmd) =>
                {
                    var file = cmd.FetchValue("--file");
                    if(file == "")
                        throw new Exception("The argument 'file' is empty.");
                    
                    Console.WriteLine("=> Source file: {0}", file);
                    
                    // eokas_main(file, llvm_jit);
                });
		
            try
            {
                program.Execute(args);
                return 0;
            }
            catch(Exception e)
            {
                Console.WriteLine("ERROR: {0}", e.Message);
                return -1;
            }
        }

        static void EokasMain(string fileName)
        {
            /*
            static void eokas_main(const String& fileName, bool(*proc)(ast_node_module_t* m))
            {
                FileStream in(fileName, "rb");
                if(!in.open())
                return;
	
                size_t size = in.size();
                MemoryBuffer buffer(size);
                    in.read(buffer.data(), buffer.size());
                    in.close();
	
                String source((const char*) buffer.data(), buffer.size());
                printf("=> Source code:\n");
                printf("------------------------------------------\n");
                printf("%s\n", source.replace("%", "%%").cstr());
                printf("------------------------------------------\n");
	
                parser_t parser;
                ast_node_module_t* m = parser.parse(source.cstr());
                printf("=> Module AST: %llX\n", (u64_t)m);
                if(m == nullptr)
                {
                    const String& error = parser.error();
                    printf("ERROR: %s\n", error.cstr());
                    return;
                }
	
                FileStream out(String::format("%s.ll", fileName.cstr()), "w+");
                if(!out.open())
                return;
	
                printf("=> Encode to IR:\n");
                printf("------------------------------------------\n");
                proc(m);
                printf("------------------------------------------\n");
                    out.close();
            }
            */
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