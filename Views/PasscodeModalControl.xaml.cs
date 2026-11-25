using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using XenomOptimizer.ViewModels;

namespace XenomOptimizer.Views;

public partial class PasscodeModalControl : UserControl
{
    private string _enteredCode = string.Empty;
    private const string TARGET_PASSCODE = "10105";
    private bool _isVerifying = false;

    private static readonly SolidColorBrush BorderDefault = new(Color.FromRgb(0x23, 0x2C, 0x3E));
    private static readonly SolidColorBrush BorderActive = new(Color.FromRgb(0x38, 0xBD, 0xF8));
    private static readonly SolidColorBrush BorderSuccess = new(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly SolidColorBrush BorderError = new(Color.FromRgb(0xEF, 0x44, 0x44));

    public PasscodeModalControl()
    {
        InitializeComponent();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (Visibility == Visibility.Visible)
        {
            ResetState();
            Focus();
            Keyboard.Focus(this);
        }
    }

    private void ResetState()
    {
        _enteredCode = string.Empty;
        _isVerifying = false;
        StatusMessage.Text = string.Empty;
        StatusMessage.Foreground = BorderError;
        LockIconText.Text = "\uE72E";
        LockIconText.Foreground = BorderActive;
        UpdateCells();
    }

    private void UpdateCells()
    {
        var cells = new[] { Cell0, Cell1, Cell2, Cell3, Cell4 };
        var txts = new[] { Txt0, Txt1, Txt2, Txt3, Txt4 };

        for (int i = 0; i < 5; i++)
        {
            if (i < _enteredCode.Length)
            {
                txts[i].Text = _enteredCode[i].ToString();
                cells[i].BorderBrush = BorderActive;
                cells[i].Background = new SolidColorBrush(Color.FromRgb(0x16, 0x20, 0x32));
            }
            else
            {
                txts[i].Text = string.Empty;
                cells[i].BorderBrush = (i == _enteredCode.Length) ? BorderActive : BorderDefault;
                cells[i].Background = new SolidColorBrush(Color.FromRgb(0x12, 0x16, 0x22));
            }
        }
    }

    private void AppendDigit(char c)
    {
        if (_isVerifying || _enteredCode.Length >= 5) return;

        _enteredCode += c;
        UpdateCells();

        if (_enteredCode.Length == 5)
        {
            ValidatePasscode();
        }
    }

    private void RemoveDigit()
    {
        if (_isVerifying || _enteredCode.Length == 0) return;

        _enteredCode = _enteredCode[..^1];
        StatusMessage.Text = string.Empty;
        UpdateCells();
    }

    private async void ValidatePasscode()
    {
        _isVerifying = true;

        if (_enteredCode == TARGET_PASSCODE)
        {
            // Success
            var cells = new[] { Cell0, Cell1, Cell2, Cell3, Cell4 };
            foreach (var cell in cells)
            {
                cell.BorderBrush = BorderSuccess;
                cell.Background = new SolidColorBrush(Color.FromRgb(0x11, 0x2E, 0x24));
            }

            StatusMessage.Foreground = BorderSuccess;
            StatusMessage.Text = "✓ Passcode Accepted! Unlocking...";
            LockIconText.Text = "\uE785"; // Unlocked icon
            LockIconText.Foreground = BorderSuccess;

            await Task.Delay(260);

            if (DataContext is MainViewModel mvm)
            {
                mvm.VerifyPasscode(TARGET_PASSCODE);
            }
            else if (Window.GetWindow(this)?.DataContext is MainViewModel winMvm)
            {
                winMvm.VerifyPasscode(TARGET_PASSCODE);
            }

            ResetState();
        }
        else
        {
            // Error
            var cells = new[] { Cell0, Cell1, Cell2, Cell3, Cell4 };
            foreach (var cell in cells)
            {
                cell.BorderBrush = BorderError;
                cell.Background = new SolidColorBrush(Color.FromRgb(0x28, 0x14, 0x18));
            }

            StatusMessage.Foreground = BorderError;
            StatusMessage.Text = "Incorrect passcode. Please try again.";

            PlayShakeAnimation();

            await Task.Delay(400);
            _enteredCode = string.Empty;
            _isVerifying = false;
            UpdateCells();
        }
    }

    private void PlayShakeAnimation()
    {
        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(-14, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(14, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(-10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(-5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350))));

        CardTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void Numpad_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string text && text.Length == 1 && char.IsDigit(text[0]))
        {
            AppendDigit(text[0]);
        }
    }

    private void Backspace_Click(object sender, RoutedEventArgs e)
    {
        RemoveDigit();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DismissModal();
    }

    private void Backdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DismissModal();
    }

    private void DismissModal()
    {
        ResetState();
        if (DataContext is MainViewModel mvm)
        {
            mvm.ClosePasscodeModal();
        }
        else if (Window.GetWindow(this)?.DataContext is MainViewModel winMvm)
        {
            winMvm.ClosePasscodeModal();
        }
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key >= Key.D0 && e.Key <= Key.D9)
        {
            AppendDigit((char)('0' + (e.Key - Key.D0)));
            e.Handled = true;
        }
        else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
        {
            AppendDigit((char)('0' + (e.Key - Key.NumPad0)));
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            RemoveDigit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DismissModal();
            e.Handled = true;
        }
    }
}
