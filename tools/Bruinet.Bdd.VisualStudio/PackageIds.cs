namespace Bruinet.Bdd.VisualStudio;

// The GUIDs and ids of BruinetBddPackage.vsct and the package attributes; kept in step with the .vsct.
internal static class PackageIds
{
    public const string Package = "3f45aa33-e8c9-4927-830b-6ba9b74c9467";

    public const string CommandSetString = "68a7da07-ac82-47a1-bd70-257d5ba3b27c";

    public const string ScenarioPreviewWindow = "8432b10b-2c5f-4c6b-af14-eda029606afb";

    public const string BddExplorerWindow = "5d2f7c61-3e0b-4b8e-9a44-6b1f0e7c2d93";

    // Active when a feature or step file is selected or open in the editor: the package loads then.
    public const string FeatureFileContext = "d446c026-dd42-46af-a662-6f72d0dbf00d";

    public const int ShowScenarioPreview = 0x0100;

    public const int OpenScenarioPreview = 0x0101;

    public const int ToggleStepBreakpoint = 0x0102;

    public const int ShowBddExplorer = 0x0103;

    public const int StepOverBdd = 0x0104;

    public const int StepIntoBdd = 0x0105;

    public const int StepOutBdd = 0x0106;

    public static readonly Guid CommandSet = new(CommandSetString);
}
