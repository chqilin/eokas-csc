namespace Eokas;

public class Option
{
    public String name = "";
    public String info = "";
    public string value = "false";

    public String toString()
    {
	    return String.Format("\t{0}\t\t\t\t{1} (default:{2})\n", name, info, value);
    }
};

public class Command
{
	public delegate void Func(Command cmd);

	public String name;
	public String info;
	public Dictionary<String, Option> options;
	public Func func;

	public Dictionary<String, Command> subCommands;

	public Command()
	{
		this.name = "";
		this.info = "";
		this.options = new Dictionary<string, Option>();
		this.func = null;
		this.subCommands = new Dictionary<string, Command>();
	}
	
	public Command(String name, String info = "")
	{
		this.name = name;
		this.info = info;
		this.options = new Dictionary<string, Option>();
		this.func = null;
		this.subCommands = new Dictionary<string, Command>();
	}
		
	public Command Option(String name, String info, String defaultValue)
	{
		Option opt = new Option();
		opt.name = name;
		opt.info = info;
		opt.value = defaultValue;
		this.options.Add(name, opt);
	
		return this;
	}

	public Command Action(Func func)
	{
		this.func = func;
		return this;
	}

	public Command SubCommand(String name, String info)
	{
		Command cmd = new Command(name, info);
		this.subCommands.Add(name, cmd);
		return cmd;
	}

	public String FetchValue(String shortName)
	{
		var opt = this.FetchOption(shortName);
		if(opt == null)
			return "";
		return opt.value;
	}

	public Option? FetchOption(String shortName)
	{
		foreach(var item in this.options)
		{
			var fragments = item.Key.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			if (fragments.Contains(shortName))
				return item.Value;
		}
		return null;
	}

	public Command? FetchCommand(String shortName)
	{
		foreach(var item in this.subCommands)
		{
			var fragments = item.Key.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			if (fragments.Contains(shortName))
				return item.Value;
		}
		
		return null;
	}

	public String toString()
	{
		String str = String.Format("{0}\t\t\t\t{1}\n", name, info);
		foreach(var opt in this.options)
		{
			str += opt.Value.toString();
		}
		foreach(var cmd in this.subCommands)
		{
			str += cmd.Value.toString();
		}
		return str;
	}

	public void Execute(string[] args)
	{
		if (args == null || args.Length == 0 || this.name != args[0])
			throw new Exception("Invalid Arguments");
		
		bool isArgumentsConsumedByCommands = false;
		bool isArgumentsConsumedByOptions = false;
	
		// process sub-commands.
		if(args.Length > 1 && this.subCommands.Count > 0)
		{
			String cmdName = args[1];
			var cmd = this.FetchCommand(cmdName);
			if (cmd != null)
			{
				isArgumentsConsumedByCommands = true;
				
				string[] subArgs = new string[args.Length - 1];
				for (int i = 0; i < subArgs.Length; i++)
				{
					subArgs[i] = args[i + 1];
				}
				
				cmd.Execute(subArgs);
			}
		}
	
		// process options.
		if(args.Length > 1 && this.options.Count > 0)
		{
			foreach(var opt in this.options)
			{
				// compatible with "-v,--version"
				var fragments = opt.Key.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < fragments.Length; i++)
				{
					var frag = fragments[i];

					int index = Array.FindIndex(args, (arg) => arg == frag);
					if (index < 0 || index >= args.Length)
						continue;

					int next = index + 1;

					opt.Value.value = next == args.Length || args[next].StartsWith("-")
						? "true"
						: args[next];
					
					isArgumentsConsumedByOptions = true;
				}
			}
		}
	
		// Only has commands but arguments were not consumed by commands.
		if(this.subCommands.Count > 0 && this.options.Count <= 0 && !isArgumentsConsumedByCommands)
			throw new Exception("Invalid Arguments.");

		// Has options but arguments didn't consumed by options.
		if(this.options.Count > 0 && !isArgumentsConsumedByOptions)
			throw new Exception("Invalid arguments");

		if (this.func != null)
		{
			this.func(this);
		}
	}
};