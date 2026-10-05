using System.Windows;
using DeskStickyNotes.Models;

namespace DeskStickyNotes.Views;

public partial class RenameNoteWindow : Window
{
    public RenameNoteWindow(string initialTitle)
    {
        InitializeComponent();
        TitleTextBox.Text = NoteModel.NormalizeTitle(initialTitle);
        Loaded += (_, _) =>
        {
            TitleTextBox.Focus();
            TitleTextBox.SelectAll();
        };
    }

    public string NoteTitle => NoteModel.NormalizeTitle(TitleTextBox.Text);

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
