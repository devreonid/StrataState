using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace StarataState
{
    public partial class MainWindow : Window
    {
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38; // Windows 11 22H2
        private const int DWMWA_USE_MICA = 1029; // Fallback Windows 11 21H2
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public class TraceStep
        {
            public int StepNumber { get; set; }
            public string CurrentState { get; set; }
            public string UnreadInput { get; set; }
            public string StackContent { get; set; }
            public string ActionDescription { get; set; }
        }

        private class TransitionRule
        {
            public string CurrentState { get; set; }
            public char? InputChar { get; set; }
            public char StackTop { get; set; }
            public string NextState { get; set; }
            public string PushToStack { get; set; }
        }

        private List<TransitionRule> _rules;

        public MainWindow()
        {
            InitializeComponent();
            InitializePDARules();

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;

            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTheme();
            EnableMica();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                Application.Current.Dispatcher.Invoke(() => ApplyTheme());
            }
        }

        private void ApplyTheme()
        {
            bool isDark = IsWindowsDarkMode();

            var helper = new WindowInteropHelper(this);
            int darkVal = isDark ? 1 : 0;
            DwmSetWindowAttribute(helper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkVal, sizeof(int));

            Color bgColor = isDark ? Color.FromArgb(255, 17, 17, 17) : Color.FromArgb(255, 250, 250, 250);

            if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000)
            {
                bgColor = isDark ? Color.FromArgb(0, 17, 17, 17) : Color.FromArgb(0, 250, 250, 250);
            }

            Color cardColor = isDark ? Color.FromRgb(24, 24, 24) : Color.FromRgb(255, 255, 255);
            Color textColor = isDark ? Color.FromRgb(237, 237, 237) : Color.FromRgb(17, 17, 17);
            Color mutedTextColor = isDark ? Color.FromRgb(160, 160, 160) : Color.FromRgb(102, 102, 102);
            Color borderColor = isDark ? Color.FromRgb(51, 51, 51) : Color.FromRgb(234, 234, 234);
            Color inputBgColor = isDark ? Color.FromRgb(17, 17, 17) : Color.FromRgb(255, 255, 255);
            Color hoverColor = isDark ? Color.FromArgb(20, 255, 255, 255) : Color.FromArgb(15, 0, 0, 0);
            Color selectionColor = isDark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(30, 0, 0, 0);

            Resources["BgBrush"] = new SolidColorBrush(bgColor);
            Resources["CardBrush"] = new SolidColorBrush(cardColor);
            Resources["TextBrush"] = new SolidColorBrush(textColor);
            Resources["MutedTextBrush"] = new SolidColorBrush(mutedTextColor);
            Resources["BorderBrush"] = new SolidColorBrush(borderColor);
            Resources["InputBgBrush"] = new SolidColorBrush(inputBgColor);
            Resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0, 112, 243));
            Resources["HoverBrush"] = new SolidColorBrush(hoverColor);
            Resources["SelectionBrush"] = new SolidColorBrush(selectionColor);
        }

        private bool IsWindowsDarkMode()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key?.GetValue("AppsUseLightTheme") != null)
                    {
                        return (int)key.GetValue("AppsUseLightTheme") == 0;
                    }
                }
            }
            catch { }
            return false;
        }

        private void EnableMica()
        {
            if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000)
            {
                var helper = new WindowInteropHelper(this);

                int backdropType = 2;
                int result = DwmSetWindowAttribute(helper.Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));

                if (result != 0)
                {
                    int trueValue = 1;
                    DwmSetWindowAttribute(helper.Handle, DWMWA_USE_MICA, ref trueValue, sizeof(int));
                }
            }
        }

        private void InitializePDARules()
        {
            _rules = new List<TransitionRule>
            {
                new TransitionRule { CurrentState = "q0", InputChar = 'a', StackTop = 'Z', NextState = "q0", PushToStack = "AZ" },
                new TransitionRule { CurrentState = "q0", InputChar = 'a', StackTop = 'A', NextState = "q0", PushToStack = "AA" },
                new TransitionRule { CurrentState = "q0", InputChar = 'b', StackTop = 'A', NextState = "q1", PushToStack = "" },
                new TransitionRule { CurrentState = "q1", InputChar = 'b', StackTop = 'A', NextState = "q1", PushToStack = "" },
                new TransitionRule { CurrentState = "q1", InputChar = null, StackTop = 'Z', NextState = "q2", PushToStack = "Z" }
            };
        }

        private void BtnCheck_Click(object sender, RoutedEventArgs e)
        {
            string input = InputTextBox.Text.Trim();
            ProcessPDA(input);
        }

        private void ProcessPDA(string inputString)
        {
            Stack<char> pdaStack = new Stack<char>();
            pdaStack.Push('Z');
            string currentState = "q0";
            int inputIndex = 0;
            int stepCounter = 1;
            List<TraceStep> traceLog = new List<TraceStep>();

            traceLog.Add(new TraceStep { StepNumber = stepCounter++, CurrentState = currentState, UnreadInput = inputString, StackContent = "Z", ActionDescription = "Initial" });

            bool isRejected = false;

            while (true)
            {
                char stackTop = pdaStack.Count > 0 ? pdaStack.Peek() : '\0';
                char? currentInput = inputIndex < inputString.Length ? (char?)inputString[inputIndex] : null;

                var matchedRule = _rules.FirstOrDefault(r => r.CurrentState == currentState && r.StackTop == stackTop && r.InputChar == currentInput) ??
                                  _rules.FirstOrDefault(r => r.CurrentState == currentState && r.StackTop == stackTop && r.InputChar == null);

                if (matchedRule == null)
                {
                    if (inputIndex < inputString.Length || currentState != "q2") isRejected = true;
                    break;
                }

                pdaStack.Pop();
                for (int i = matchedRule.PushToStack.Length - 1; i >= 0; i--) pdaStack.Push(matchedRule.PushToStack[i]);

                currentState = matchedRule.NextState;
                string actionDesc = $"Push({(matchedRule.PushToStack == "" ? "ε" : matchedRule.PushToStack)})";

                if (matchedRule.InputChar != null) { inputIndex++; actionDesc = $"Read '{matchedRule.InputChar}', " + actionDesc; }
                else { actionDesc = "Epsilon, " + actionDesc; }

                traceLog.Add(new TraceStep
                {
                    StepNumber = stepCounter++,
                    CurrentState = currentState,
                    UnreadInput = inputIndex < inputString.Length ? inputString.Substring(inputIndex) : "ε",
                    StackContent = pdaStack.Count > 0 ? string.Join("", pdaStack.ToArray()) : "ε",
                    ActionDescription = actionDesc
                });

                if (currentState == "q2" && inputIndex == inputString.Length) break;
            }

            bool isAccepted = !isRejected && currentState == "q2" && inputIndex == inputString.Length;
            TraceDataGrid.ItemsSource = traceLog;

            ResultBorder.Visibility = Visibility.Visible;
            if (isAccepted)
            {
                ResultTextBlock.Text = "Accepted ✓";
                ResultTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0, 112, 243));
                ResultBorder.Background = new SolidColorBrush(Color.FromArgb(30, 0, 112, 243));
            }
            else
            {
                ResultTextBlock.Text = "Rejected ✗";
                ResultTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(224, 0, 0));
                ResultBorder.Background = new SolidColorBrush(Color.FromArgb(30, 224, 0, 0));
            }
        }
    }
}