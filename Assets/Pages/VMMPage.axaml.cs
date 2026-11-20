using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace AvaloniaApplication2.Assets.Pages
{
    public partial class VMMPage : UserControl
    {
        // Theme brushes
        private static readonly IBrush LimeBrush = new SolidColorBrush(Color.Parse("#00A844"));
        private static readonly IBrush BorderBrushGreen = new SolidColorBrush(Color.Parse("#005C25"));
        private static readonly IBrush ActiveBorderBrush = new SolidColorBrush(Color.Parse("#00FF66"));
        private static readonly IBrush DarkButtonBrush = new SolidColorBrush(Color.Parse("#181818"));
        private static readonly IBrush BlackBrush = new SolidColorBrush(Color.Parse("#0A0A0A"));
        private static readonly IBrush WhiteBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));

        public event Action<string, string>? SaveRequested;
        public event Action? CancelRequested;

        private enum FieldTarget
        {
            MachineId,
            MachineName
        }

        private FieldTarget _currentTarget = FieldTarget.MachineId;
        private bool _isCaps = true;
        private readonly List<Button> _letterButtons = new List<Button>();

        public VMMPage()
        {
            InitializeComponent();

            // Wire Tab buttons
            EditIdTabButton.Click += (_, _) => SetActiveField(FieldTarget.MachineId);
            EditNameTabButton.Click += (_, _) => SetActiveField(FieldTarget.MachineName);

            // Wire TextBox Focus
            MachineIdTextBox.GotFocus += (_, _) => SetActiveField(FieldTarget.MachineId);
            MachineNameTextBox.GotFocus += (_, _) => SetActiveField(FieldTarget.MachineName);

            // Wire Save & Cancel
            SaveButton.Click += (_, _) =>
            {
                string machineId = MachineIdTextBox.Text?.Trim() ?? "";
                string machineName = MachineNameTextBox.Text?.Trim() ?? "";
                SaveRequested?.Invoke(machineId, machineName);
            };

            CancelButton.Click += (_, _) =>
            {
                CancelRequested?.Invoke();
            };

            // Wire Keypad function buttons
            CapsButton.Click += KeypadCaps_Click;
            BackspaceButton.Click += KeypadBackspace_Click;
            SpaceButton.Click += (_, _) => InsertText(" ");
            KeypadClearButton.Click += KeypadClear_Click;
            NextFieldButton.Click += KeypadNextField_Click;

            // Wire Keypad character buttons
            WirePanelButtons(RowNumbers);
            WirePanelButtons(RowLetters1);
            WirePanelButtons(RowLetters2);
            WirePanelButtons(RowLetters3);
            WirePanelButtons(RowBottom);

            // Set initial field and visuals
            SetActiveField(FieldTarget.MachineId);
            UpdateCapsVisual();
        }

        public void SetValues(string machineId, string machineName)
        {
            MachineIdTextBox.Text = machineId ?? "";
            MachineNameTextBox.Text = machineName ?? "";
        }

        // =========================================================
        // ON-SCREEN KEYPAD LOGIC
        // =========================================================

        private void WirePanelButtons(Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Button btn && btn.Tag is string tag && !string.IsNullOrEmpty(tag))
                {
                    btn.Click += (_, _) => KeypadChar_Click(tag);
                    if (btn.Classes.Contains("keypadLetter"))
                    {
                        _letterButtons.Add(btn);
                    }
                }
            }
        }

        private TextBox GetActiveTextBox()
        {
            return _currentTarget == FieldTarget.MachineName ? MachineNameTextBox : MachineIdTextBox;
        }

        private void SetActiveField(FieldTarget target)
        {
            _currentTarget = target;

            ActiveFieldLabel.Text = target switch
            {
                FieldTarget.MachineId => "EDITING: MACHINE ID",
                FieldTarget.MachineName => "EDITING: MACHINE NAME",
                _ => "EDITING"
            };

            UpdateTabVisuals();
            UpdateTextBoxVisuals();
        }

        private void UpdateTabVisuals()
        {
            EditIdTabButton.Background = _currentTarget == FieldTarget.MachineId ? LimeBrush : DarkButtonBrush;
            EditIdTabButton.Foreground = _currentTarget == FieldTarget.MachineId ? BlackBrush : WhiteBrush;
            EditIdTabButton.BorderBrush = _currentTarget == FieldTarget.MachineId ? ActiveBorderBrush : BorderBrushGreen;

            EditNameTabButton.Background = _currentTarget == FieldTarget.MachineName ? LimeBrush : DarkButtonBrush;
            EditNameTabButton.Foreground = _currentTarget == FieldTarget.MachineName ? BlackBrush : WhiteBrush;
            EditNameTabButton.BorderBrush = _currentTarget == FieldTarget.MachineName ? ActiveBorderBrush : BorderBrushGreen;
        }

        private void UpdateTextBoxVisuals()
        {
            MachineIdTextBox.BorderBrush = _currentTarget == FieldTarget.MachineId ? ActiveBorderBrush : BorderBrushGreen;
            MachineNameTextBox.BorderBrush = _currentTarget == FieldTarget.MachineName ? ActiveBorderBrush : BorderBrushGreen;
        }

        private void KeypadChar_Click(string tag)
        {
            string charToInsert = _isCaps ? tag.ToUpperInvariant() : tag.ToLowerInvariant();
            InsertText(charToInsert);
        }

        private void InsertText(string textToInsert)
        {
            TextBox target = GetActiveTextBox();
            string current = target.Text ?? "";
            int caret = target.CaretIndex;

            if (caret < 0 || caret > current.Length)
            {
                caret = current.Length;
            }

            string updated = current.Insert(caret, textToInsert);
            target.Text = updated;
            target.CaretIndex = caret + textToInsert.Length;
        }

        private void KeypadBackspace_Click(object? sender, RoutedEventArgs e)
        {
            TextBox target = GetActiveTextBox();
            string current = target.Text ?? "";
            if (current.Length == 0) return;

            int caret = target.CaretIndex;
            if (caret <= 0 || caret > current.Length)
            {
                caret = current.Length;
            }

            if (caret > 0)
            {
                string updated = current.Remove(caret - 1, 1);
                target.Text = updated;
                target.CaretIndex = caret - 1;
            }
        }

        private void KeypadClear_Click(object? sender, RoutedEventArgs e)
        {
            TextBox target = GetActiveTextBox();
            target.Text = "";
            target.CaretIndex = 0;
        }

        private void KeypadNextField_Click(object? sender, RoutedEventArgs e)
        {
            FieldTarget next = _currentTarget == FieldTarget.MachineId ? FieldTarget.MachineName : FieldTarget.MachineId;
            SetActiveField(next);
        }

        private void KeypadCaps_Click(object? sender, RoutedEventArgs e)
        {
            _isCaps = !_isCaps;
            UpdateCapsVisual();
        }

        private void UpdateCapsVisual()
        {
            if (CapsButton != null)
            {
                CapsButton.Background = _isCaps ? LimeBrush : DarkButtonBrush;
                CapsButton.Foreground = _isCaps ? BlackBrush : WhiteBrush;
                CapsButton.BorderBrush = _isCaps ? ActiveBorderBrush : BorderBrushGreen;
            }

            foreach (var btn in _letterButtons)
            {
                if (btn.Tag is string tag)
                {
                    btn.Content = _isCaps ? tag.ToUpperInvariant() : tag.ToLowerInvariant();
                }
            }
        }
    }
}