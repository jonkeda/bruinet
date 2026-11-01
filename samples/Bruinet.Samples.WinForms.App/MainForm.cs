namespace Bruinet.Samples.WinForms.App;

partial class MainForm : Form
{
    public MainForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Bruinet WinForms Sample Application";
        this.Size = new System.Drawing.Size(600, 900);
        this.StartPosition = FormStartPosition.CenterScreen;

        // Create a panel to hold all controls
        var panel = new Panel();
        panel.Dock = DockStyle.Fill;
        panel.AutoScroll = true;
        panel.Padding = new Padding(10);

        int yPosition = 10;

        // Username Label and TextBox
        var userLabel = new Label();
        userLabel.Text = "Username:";
        userLabel.Location = new System.Drawing.Point(10, yPosition);
        userLabel.Size = new System.Drawing.Size(100, 20);
        userLabel.Name = "lblUsername";
        panel.Controls.Add(userLabel);

        var usernameBox = new TextBox();
        usernameBox.Location = new System.Drawing.Point(120, yPosition);
        usernameBox.Size = new System.Drawing.Size(200, 20);
        usernameBox.Name = "txtUsername";
        usernameBox.Text = "";
        panel.Controls.Add(usernameBox);
        yPosition += 30;

        // Password Label and TextBox
        var passLabel = new Label();
        passLabel.Text = "Password:";
        passLabel.Location = new System.Drawing.Point(10, yPosition);
        passLabel.Size = new System.Drawing.Size(100, 20);
        passLabel.Name = "lblPassword";
        panel.Controls.Add(passLabel);

        var passwordBox = new TextBox();
        passwordBox.Location = new System.Drawing.Point(120, yPosition);
        passwordBox.Size = new System.Drawing.Size(200, 20);
        passwordBox.PasswordChar = '*';
        passwordBox.Name = "txtPassword";
        panel.Controls.Add(passwordBox);
        yPosition += 30;

        // Remember Me Checkbox
        var rememberCheckBox = new CheckBox();
        rememberCheckBox.Text = "Remember me";
        rememberCheckBox.Location = new System.Drawing.Point(120, yPosition);
        rememberCheckBox.Size = new System.Drawing.Size(150, 20);
        rememberCheckBox.Name = "chkRemember";
        panel.Controls.Add(rememberCheckBox);
        yPosition += 30;

        // Role Selection ComboBox
        var roleLabel = new Label();
        roleLabel.Text = "Role:";
        roleLabel.Location = new System.Drawing.Point(10, yPosition);
        roleLabel.Size = new System.Drawing.Size(100, 20);
        roleLabel.Name = "lblRole";
        panel.Controls.Add(roleLabel);

        var roleCombo = new ComboBox();
        roleCombo.Location = new System.Drawing.Point(120, yPosition);
        roleCombo.Size = new System.Drawing.Size(200, 20);
        roleCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        roleCombo.Items.AddRange(new object[] { "Admin", "User", "Guest" });
        roleCombo.SelectedIndex = 0;
        roleCombo.Name = "cmbRole";
        panel.Controls.Add(roleCombo);
        yPosition += 30;

        // Port (Numeric) Label and NumericUpDown
        var portLabel = new Label();
        portLabel.Text = "Port:";
        portLabel.Location = new System.Drawing.Point(10, yPosition);
        portLabel.Size = new System.Drawing.Size(100, 20);
        portLabel.Name = "lblPort";
        panel.Controls.Add(portLabel);

        var portNumeric = new NumericUpDown();
        portNumeric.Location = new System.Drawing.Point(120, yPosition);
        portNumeric.Size = new System.Drawing.Size(200, 20);
        portNumeric.Minimum = 1;
        portNumeric.Maximum = 65535;
        portNumeric.Value = 8080;
        portNumeric.Name = "nudPort";
        panel.Controls.Add(portNumeric);
        yPosition += 30;

        // Notes (Rich Text) Label and RichTextBox
        var notesLabel = new Label();
        notesLabel.Text = "Notes:";
        notesLabel.Location = new System.Drawing.Point(10, yPosition);
        notesLabel.Size = new System.Drawing.Size(100, 20);
        notesLabel.Name = "lblNotes";
        panel.Controls.Add(notesLabel);
        yPosition += 20;

        var notesRichText = new RichTextBox();
        notesRichText.Location = new System.Drawing.Point(10, yPosition);
        notesRichText.Size = new System.Drawing.Size(310, 80);
        notesRichText.Name = "rtbNotes";
        notesRichText.Text = "Enter any additional notes here...";
        panel.Controls.Add(notesRichText);
        yPosition += 90;

        // Start Date Label and DateTimePicker
        var startDateLabel = new Label();
        startDateLabel.Text = "Start Date:";
        startDateLabel.Location = new System.Drawing.Point(10, yPosition);
        startDateLabel.Size = new System.Drawing.Size(100, 20);
        startDateLabel.Name = "lblStartDate";
        panel.Controls.Add(startDateLabel);

        var startDatePicker = new DateTimePicker();
        startDatePicker.Location = new System.Drawing.Point(120, yPosition);
        startDatePicker.Size = new System.Drawing.Size(200, 20);
        startDatePicker.Format = System.Windows.Forms.DateTimePickerFormat.Short;
        startDatePicker.Name = "dtpStartDate";
        startDatePicker.Value = System.DateTime.Today;
        panel.Controls.Add(startDatePicker);
        yPosition += 30;

        // Volume Label and TrackBar
        var volumeLabel = new Label();
        volumeLabel.Text = "Volume:";
        volumeLabel.Location = new System.Drawing.Point(10, yPosition);
        volumeLabel.Size = new System.Drawing.Size(100, 20);
        volumeLabel.Name = "lblVolume";
        panel.Controls.Add(volumeLabel);

        var volumeTrackBar = new TrackBar();
        volumeTrackBar.Location = new System.Drawing.Point(120, yPosition);
        volumeTrackBar.Size = new System.Drawing.Size(200, 40);
        volumeTrackBar.Minimum = 0;
        volumeTrackBar.Maximum = 100;
        volumeTrackBar.Value = 50;
        volumeTrackBar.Name = "trbVolume";
        panel.Controls.Add(volumeTrackBar);
        yPosition += 50;

        // Progress Label and ProgressBar
        var progressLabel = new Label();
        progressLabel.Text = "Progress:";
        progressLabel.Location = new System.Drawing.Point(10, yPosition);
        progressLabel.Size = new System.Drawing.Size(100, 20);
        progressLabel.Name = "lblProgress";
        panel.Controls.Add(progressLabel);

        var progressBar = new ProgressBar();
        progressBar.Location = new System.Drawing.Point(120, yPosition);
        progressBar.Size = new System.Drawing.Size(200, 20);
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        progressBar.Value = 0;
        progressBar.Name = "prbProgress";
        panel.Controls.Add(progressBar);
        yPosition += 30;

        // Status Label
        var statusLabel = new Label();
        statusLabel.Text = "Status: Ready";
        statusLabel.Location = new System.Drawing.Point(10, yPosition);
        statusLabel.Size = new System.Drawing.Size(300, 20);
        statusLabel.Name = "lblStatus";
        panel.Controls.Add(statusLabel);
        yPosition += 30;

        // Login Button
        var loginButton = new Button();
        loginButton.Text = "Login";
        loginButton.Location = new System.Drawing.Point(120, yPosition);
        loginButton.Size = new System.Drawing.Size(100, 30);
        loginButton.Name = "btnLogin";
        loginButton.Click += (sender, e) =>
        {
            var username = usernameBox.Text;
            var role = roleCombo.SelectedItem?.ToString() ?? "Unknown";
            var port = portNumeric.Value;
            statusLabel.Text = $"Status: Logged in as {username} ({role}) on port {port}";
            statusLabel.ForeColor = System.Drawing.Color.Green;
        };
        panel.Controls.Add(loginButton);

        // Clear Button
        var clearButton = new Button();
        clearButton.Text = "Clear";
        clearButton.Location = new System.Drawing.Point(230, yPosition);
        clearButton.Size = new System.Drawing.Size(100, 30);
        clearButton.Name = "btnClear";
        clearButton.Click += (sender, e) =>
        {
            usernameBox.Clear();
            passwordBox.Clear();
            rememberCheckBox.Checked = false;
            roleCombo.SelectedIndex = 0;
            portNumeric.Value = 8080;
            notesRichText.Clear();
            statusLabel.Text = "Status: Ready";
            statusLabel.ForeColor = System.Drawing.SystemColors.ControlText;
        };
        panel.Controls.Add(clearButton);
        yPosition += 40;

        // Items ListBox
        var itemsLabel = new Label();
        itemsLabel.Text = "Items:";
        itemsLabel.Location = new System.Drawing.Point(10, yPosition);
        itemsLabel.Size = new System.Drawing.Size(100, 20);
        itemsLabel.Name = "lblItems";
        panel.Controls.Add(itemsLabel);
        yPosition += 20;

        var itemsListBox = new ListBox();
        itemsListBox.Location = new System.Drawing.Point(10, yPosition);
        itemsListBox.Size = new System.Drawing.Size(310, 100);
        itemsListBox.Name = "lstItems";
        itemsListBox.Items.AddRange(new object[] { "Item 1", "Item 2", "Item 3", "Item 4", "Item 5" });
        panel.Controls.Add(itemsListBox);

        this.Controls.Add(panel);
    }
}
