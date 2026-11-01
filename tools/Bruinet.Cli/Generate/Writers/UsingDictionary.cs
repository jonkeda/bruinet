using System.Text;
using System.Globalization;

namespace Bruinet.Generator.Writers;

public class VbImportsDictionary
{
	private Dictionary<string, string> VbImports = new Dictionary<string, string>();
	public int Count
	{
		get { return VbImports.Count; }
	}

	public void Add(string aUsing)
	{
		if (!VbImports.ContainsKey(aUsing))
		{
			VbImports.Add(aUsing, aUsing);
		}
	}

	public override string ToString()
	{
		var sb = new StringBuilder();

		foreach (string key in VbImports.Keys)
		{
			sb.AppendFormat(CultureInfo.InvariantCulture, "Imports {0}\n", key);
		}

		return sb.ToString();
	}
}


public class UsingDictionary
{
	private Dictionary<string, string> Usings = new Dictionary<string, string>();
	public int Count
	{
		get { return Usings.Count; }
	}

	public void Add(string aUsing)
	{
		if (!Usings.ContainsKey(aUsing))
		{
			Usings.Add(aUsing, aUsing);
		}
	}

	public override string ToString()
	{
		var sb = new StringBuilder();

		foreach (string key in Usings.Keys)
		{
			sb.AppendFormat(CultureInfo.InvariantCulture, "using {0};\n", key);
		}

		return sb.ToString();
	}
}
