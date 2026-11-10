using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;

namespace Bruinet.Bdd.Workspace.UITests.PageObjects;

/// <summary>The Scenario Preview page, as Visual Studio's and VS Code's webviews show it.</summary>
public sealed class ScenarioPreviewPage(IHtmlTestContext context) : PageObjectBase<ScenarioPreviewPage>(context)
{
    /// <summary>"Parsed only", or "Bound in" a suite.</summary>
    public LabelControl<ScenarioPreviewPage> BindState => new(this, "#uat-state");

    /// <summary>The bar a debugged run shows while paused at a step.</summary>
    public LabelControl<ScenarioPreviewPage> PausedBar => new(this, "#uat-paused");

    /// <summary>Where the run is paused, in words.</summary>
    public LabelControl<ScenarioPreviewPage> PausedAt => new(this, "#uat-paused-at");

    /// <summary>The stop's app, page and arguments.</summary>
    public LabelControl<ScenarioPreviewPage> PausedDetails => new(this, "#uat-paused-details");

    /// <summary>Screenshot now: on a Step-through run's stop only.</summary>
    public ButtonControl<ScenarioPreviewPage> Screenshot => new(this, "#uat-screenshot");

    /// <summary>Debug from here: on a Step-through run's stop only.</summary>
    public ButtonControl<ScenarioPreviewPage> DebugHere => new(this, "#uat-debug-here");

    public ButtonControl<ScenarioPreviewPage> Continue => new(this, "#uat-paused [data-move='continue']");

    public ButtonControl<ScenarioPreviewPage> StepOver => new(this, "#uat-paused [data-move='over']");

    public ButtonControl<ScenarioPreviewPage> StepInto => new(this, "#uat-paused [data-move='into']");

    public ButtonControl<ScenarioPreviewPage> StepOut => new(this, "#uat-paused [data-move='out']");

    public ButtonControl<ScenarioPreviewPage> Stop => new(this, "#uat-paused [data-move='stop']");

    /// <summary>The step on a line of the file.</summary>
    /// <param name="line">The step's line, 1-based.</param>
    public LabelControl<ScenarioPreviewPage> Step(int line) => new(this, $".uat-step[data-line='{line}']");

    /// <summary>A step's keyword, the gutter a click sets or clears its breakpoint in.</summary>
    /// <param name="line">The step's line, 1-based.</param>
    public ButtonControl<ScenarioPreviewPage> StepKeyword(int line) => new(this, $".uat-step[data-line='{line}'] > .uat-kw");

    /// <summary>A scenario's Run button.</summary>
    /// <param name="headingLine">The scenario heading's line, 1-based.</param>
    public ButtonControl<ScenarioPreviewPage> Run(int headingLine) => new(this, $".uat-run[data-run='{headingLine}']:not([data-debug]):not([data-test])");

    /// <summary>A scenario's Run as test button: <c>dotnet test</c>, the gate.</summary>
    /// <param name="headingLine">The scenario heading's line, 1-based.</param>
    public ButtonControl<ScenarioPreviewPage> RunAsTest(int headingLine) => new(this, $".uat-run[data-test][data-run='{headingLine}']");

    /// <summary>The file's Run button, on the title: every scenario, on the suite's runner.</summary>
    public ButtonControl<ScenarioPreviewPage> RunFile => new(this, ".uat-title .uat-run[data-run-file]:not([data-debug]):not([data-test])");

    /// <summary>A scenario's Debug button.</summary>
    /// <param name="headingLine">The scenario heading's line, 1-based.</param>
    public ButtonControl<ScenarioPreviewPage> Debug(int headingLine) => new(this, $".uat-run[data-debug][data-run='{headingLine}']");

    /// <summary>A scenario's Step button: Step through in the suite's runner.</summary>
    /// <param name="headingLine">The scenario heading's line, 1-based.</param>
    public ButtonControl<ScenarioPreviewPage> StepThrough(int headingLine) => new(this, $".uat-run[data-step='{headingLine}']");

    /// <summary>The scenario whose heading is on a line.</summary>
    /// <param name="line">The heading's line, 1-based.</param>
    public LabelControl<ScenarioPreviewPage> Scenario(int line) => new(this, $".uat-scenario[data-line='{line}']");
}
