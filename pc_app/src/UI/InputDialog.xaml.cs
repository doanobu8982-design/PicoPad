using System.Windows;
using System.Windows.Input;

namespace GamepadEmulator.UI
{
    public partial class InputDialog : Window
    {
        public string ResultText { get; private set; } = string.Empty;

        public InputDialog(string title, string prompt, string defaultText = "")
        {
            InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            InputTextBox.Text = defaultText;
            InputTextBox.SelectAll();
            Loaded += (_, _) => InputTextBox.Focus();
        }

        public static string? Show(Window owner, string title, string prompt, string defaultText = "")
        {
            var dialog = new InputDialog(title, prompt, defaultText)
            {
                Owner = owner
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                return dialog.ResultText;
            }
            return null;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Confirm();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Confirm();
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void Confirm()
        {
            ResultText = InputTextBox.Text.Trim();
            DialogResult = true;
            Close();
        }
    }
}

