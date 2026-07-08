using System.Windows;
using System.Windows.Input;

namespace DeskStickyNotes.Views;

public partial class HyperlinkWindow : Window
{
    public HyperlinkWindow(string initialText, string initialUrl = "")
    {
        InitializeComponent();

        DisplayTextBox.Text = initialText;
        UrlTextBox.Text = initialUrl;
        UpdateInsertButton();

        Loaded += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(DisplayTextBox.Text))
            {
                DisplayTextBox.Focus();
            }
            else if (string.IsNullOrWhiteSpace(UrlTextBox.Text))
            {
                UrlTextBox.Focus();
            }
            else
            {
                DisplayTextBox.Focus();
                DisplayTextBox.SelectAll();
            }
        };
    }

    public string LinkText => DisplayTextBox.Text.Trim();

    public string LinkUrl => UrlTextBox.Text.Trim();

    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LinkText))
        {
            DisplayTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(LinkUrl))
        {
            UrlTextBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Input_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateInsertButton();
    }

    private void Input_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Insert_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void UpdateInsertButton()
    {
        InsertButton.IsEnabled = !string.IsNullOrWhiteSpace(DisplayTextBox.Text)
            && !string.IsNullOrWhiteSpace(UrlTextBox.Text);
    }
}
