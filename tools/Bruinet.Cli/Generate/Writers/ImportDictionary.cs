using System.Text;
using System.Globalization;

namespace Bruinet.Generator.Writers;

public class ImportDictionary
{
	private Dictionary<string, string> Imports = new Dictionary<string, string>();

	public void Add(string import)
	{
		if (!Imports.ContainsKey(import))
		{
			Imports.Add(import, import);
		}
	}

	public string ToString(string aPackage)
	{
		var sb = new StringBuilder();

		foreach (string key in Imports.Keys.Where(i =>
				         !i.StartsWith("java.", StringComparison.Ordinal) &&
				         !i.StartsWith("javax.", StringComparison.Ordinal) &&
				         !i.StartsWith(aPackage, StringComparison.Ordinal))
			         .OrderBy(i => i).ToList())
		{
			sb.AppendFormat(CultureInfo.InvariantCulture, "using {0};\n", key);
		}

		foreach (string key in Imports.Keys.Where(i => i.StartsWith("javax.", StringComparison.Ordinal)).OrderBy(i => i).ToList())
		{
			sb.AppendFormat(CultureInfo.InvariantCulture, "using {0};\n", key);
		}

		foreach (string key in Imports.Keys.Where(i => i.StartsWith("java.", StringComparison.Ordinal)).OrderBy(i => i).ToList())
		{
			sb.AppendFormat(CultureInfo.InvariantCulture, "using {0};\n", key);
		}

		return sb.ToString();
	}
}