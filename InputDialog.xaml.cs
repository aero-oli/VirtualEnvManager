using System.Windows;

namespace VirtualEnvManager
{
    public partial class InputDialog : Window
    {
        public string InputText => InputTextBox.Text;

        public InputDialog(string prompt, string title)
        {
            InitializeComponent();
            PromptText.Text = prompt;
            Title = title;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
} 