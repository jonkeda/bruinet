namespace Bruinet.Samples.Maui.App.Navigation;

/// <summary>
/// The pages the sample app exposes, one per control module.
/// </summary>
/// <remarks>
/// Used as the stable identity of a page across the app and its UI tests: the hub derives a
/// button's <c>AutomationId</c> from it, and the test fixture derives the locator from the
/// same member. Renaming a member therefore changes both sides together rather than leaving
/// them silently out of step.
/// </remarks>
public enum SamplePage
{
    /// <summary>The greeting demo the Markdown UAT sample scenarios drive.</summary>
    Main,

    /// <summary>The profile form the Markdown UAT sample scenarios fill in.</summary>
    UserForm,

    /// <summary>Button and ImageButton controls.</summary>
    Buttons,

    /// <summary>DatePicker and TimePicker controls.</summary>
    DateTime,

    /// <summary>Label, Image, ActivityIndicator, ProgressBar.</summary>
    Display,

    /// <summary>Slider and Stepper controls.</summary>
    Range,

    /// <summary>Picker and selection controls.</summary>
    Selection,

    /// <summary>Entry, Editor, SearchBar.</summary>
    Text,

    /// <summary>CheckBox, RadioButton, Switch.</summary>
    Toggle,

    /// <summary>Grid, Border, ContentView, ScrollView container scoping.</summary>
    Container,

    /// <summary>CollectionView, ListView and other collection controls.</summary>
    Collection,

    /// <summary>Grid + CollectionView demo used by the container tests.</summary>
    GridCollection,

    /// <summary>Shape controls.</summary>
    Shapes,

    /// <summary>DisplayAlert and DisplayPrompt dialogs.</summary>
    Dialogs,

    /// <summary>Toolbar, Menu and TabMenu navigation controls, as test subjects.</summary>
    Navigation,

    /// <summary>A long ScrollView used to test scrolling on its own.</summary>
    Scroll,

    /// <summary>Tap, swipe and the verbs only the app itself can answer.</summary>
    Gestures,

    /// <summary>Phase 0 probe measuring which layouts expose their AutomationId.</summary>
    AutomationProbe,

    /// <summary>CommunityToolkit.Maui views: Expander, AvatarView, RatingView, DrawingView, StateContainer, DockLayout.</summary>
    CommunityToolkit,

    /// <summary>CommunityToolkit.Maui alerts: Popup, Snackbar and Toast.</summary>
    CommunityToolkitAlerts,

    /// <summary>CommunityToolkit.Maui MediaElement.</summary>
    CommunityToolkitMedia,

    /// <summary>A CollectionView that loads its pages as it nears its end, with a search.</summary>
    PagedList,

    /// <summary>A busy page that a pushed page and a modal cover.</summary>
    Covered
}
