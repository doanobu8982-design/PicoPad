using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GamepadEmulator.Core;
using GamepadEmulator.Models;

namespace GamepadEmulator.UI
{
    public partial class MainWindow : Window
    {
        private readonly ProfileManager _profileManager = new();
        private readonly RawInputReceiver _receiver = new();
        private readonly SerialCommunicator _serial = new();
        private readonly DeviceWatcher _deviceWatcher = new();
        private readonly DispatcherTimer _uiTimer = new();

        private GamepadProfile? _profileSnapshot;
        private DispatcherTimer? _saveStatusTimer;

        private readonly object _stateLock = new();
        private readonly byte[] _latestPacket = new byte[6];
        private InputState _latestInputState;
        private ulong _packetCounter;
        private bool _hasNewPacket;

        private PicoDeviceInfo _latestDeviceInfo = new(PicoDeviceMode.Disconnected, null);

        private readonly SolidColorBrush _activeBgBrush = new(Color.FromRgb(0xA6, 0xE3, 0xA1)); // Pastel Green
        private readonly SolidColorBrush _activeFgBrush = new(Color.FromRgb(0x11, 0x11, 0x1B)); // Dark
        private readonly SolidColorBrush _inactiveBgBrush = new(Color.FromArgb(0xD0, 0x18, 0x18, 0x25)); // Semi-transparent Dark
        private readonly SolidColorBrush _inactiveFgBrush = new(Color.FromRgb(0xFF, 0xFF, 0xFF)); // Pure White (#FFFFFF)
        private readonly SolidColorBrush _waitingBorderBrush = new(Color.FromRgb(0xF9, 0xE2, 0xAF)); // Gold
        private readonly SolidColorBrush _defaultBorderBrush = new(Color.FromRgb(0x58, 0x5B, 0x70)); // Slate
        private readonly SolidColorBrush _activeBorderBrush = new(Color.FromRgb(0xA6, 0xE3, 0xA1)); // Active Glow Border

        private string? _waitingBindingButton;

        public MainWindow()
        {
            InitializeComponent();

            // Apply high-precision timer & Windows 11 power throttling optimizations
            PowerThrottling.OptimizeProcessPowerSettings();

            CheckAdminPrivilege();
            InitializeProfiles();

            // Set up Serial and DeviceWatcher
            _serial.Connected += OnSerialConnected;
            _serial.Disconnected += OnSerialDisconnected;

            _deviceWatcher.DeviceStatusChanged += OnDeviceStatusChanged;
            _deviceWatcher.Start();

            // Start Raw Input Receiver with active profile
            _receiver.OnPacketGenerated += Receiver_OnPacketGenerated;
            _receiver.SetProfile(_profileManager.ActiveProfile);
            _receiver.Start();

            _uiTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Hook window messages to capture WM_DEVICECHANGE
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource? source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProcHook);
        }

        private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == (int)Win32Native.WM_DEVICECHANGE)
            {
                _deviceWatcher.OnDeviceChangeReceived((uint)wParam);
            }
            return IntPtr.Zero;
        }

        private void CheckAdminPrivilege()
        {
            try
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new(identity);
                bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

                if (isAdmin)
                {
                    AdminBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
                    AdminBadgeText.Text = "管理者権限: 有効 (UIPI安全)";
                    AdminBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x1B));
                }
                else
                {
                    AdminBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
                    AdminBadgeText.Text = "管理者権限: なし (UIPIによる入力遮断の可能性あり)";
                    AdminBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x1B));
                }
            }
            catch
            {
                AdminBadgeText.Text = "権限状態: 取得不可";
            }
        }

        private void InitializeProfiles()
        {
            RefreshProfileComboBox(_profileManager.ActiveProfile);
        }

        private void RefreshProfileComboBox(GamepadProfile? selectProfile = null)
        {
            ProfileComboBox.SelectionChanged -= ProfileComboBox_SelectionChanged;

            ProfileComboBox.ItemsSource = null;
            ProfileComboBox.ItemsSource = _profileManager.Profiles;

            var target = selectProfile ?? _profileManager.ActiveProfile;
            ProfileComboBox.SelectedItem = target;

            ProfileComboBox.SelectionChanged += ProfileComboBox_SelectionChanged;

            _profileSnapshot = target?.Clone();
            ApplyProfileToUI(target);
        }

        private void ApplyProfileToUI(GamepadProfile? profile)
        {
            if (profile == null)
            {
                ModeCheckBox.IsEnabled = false;
                ModeCheckBox.IsChecked = false;
                ModeDescriptionText.Text = "（プロファイルがありません）";

                _receiver.SetProfile(null);

                DuplicateProfileButton.IsEnabled = false;
                BtnRenameProfile.IsEnabled = false;
                BtnDeleteProfile.IsEnabled = false;
                BtnSaveProfile.IsEnabled = false;
                BtnRevertProfile.IsEnabled = false;

                _waitingBindingButton = null;
                RefreshAllBindingLabels();
                return;
            }

            ModeCheckBox.IsEnabled = true;
            ModeCheckBox.IsChecked = profile.IsStickMode;
            ModeDescriptionText.Text = profile.IsStickMode
                ? "ON: 左スティック出力中 (東方非想天則推奨 - バニラ移動対応)"
                : "OFF: D-Pad出力中 (SF6 / MBTL推奨 - POVハット対応)";

            _receiver.SetProfile(profile);

            // 全プロファイルが自由に変更・削除可能
            DuplicateProfileButton.IsEnabled = true;
            BtnRenameProfile.IsEnabled = true;
            BtnDeleteProfile.IsEnabled = true;
            BtnSaveProfile.IsEnabled = true;
            BtnRevertProfile.IsEnabled = true;

            RefreshAllBindingLabels();
        }

        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileComboBox.SelectedItem is GamepadProfile profile)
            {
                _profileManager.ActiveProfile = profile;
                _profileSnapshot = profile.Clone();
                ApplyProfileToUI(profile);
            }
            else
            {
                _profileSnapshot = null;
                ApplyProfileToUI(null);
            }
            Keyboard.ClearFocus();
        }

        private void ProfileComboBox_DropDownClosed(object? sender, EventArgs e)
        {
            Keyboard.ClearFocus();
        }

        private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
        {
            string? name = InputDialog.Show(this, "新規プロファイルの作成", "新しいプロファイル名を入力してください:", "新規プロファイル");
            Keyboard.ClearFocus();
            if (string.IsNullOrWhiteSpace(name)) return;

            string trimmed = name.Trim();
            if (_profileManager.IsProfileNameExists(trimmed))
            {
                MessageBox.Show(this,
                    "既に同じ名前のプロファイルが存在します。別の名前を入力してください。",
                    "プロファイル名の重複エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                var newProfile = _profileManager.CreateProfile(trimmed);
                _profileSnapshot = newProfile.Clone();
                RefreshProfileComboBox(newProfile);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "作成エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DuplicateProfileButton_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            var active = _profileManager.ActiveProfile;
            if (active == null) return;

            string defaultName = $"{active.Name} のコピー";
            if (_profileManager.IsProfileNameExists(defaultName))
            {
                int suffix = 2;
                while (_profileManager.IsProfileNameExists($"{defaultName} {suffix}"))
                {
                    suffix++;
                }
                defaultName = $"{defaultName} {suffix}";
            }

            string? name = InputDialog.Show(this, "プロファイルの複製", "複製後の新しいプロファイル名を入力してください:", defaultName);
            Keyboard.ClearFocus();
            if (name == null) return;

            string trimmed = name.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                MessageBox.Show(this,
                    "プロファイル名を空にすることはできません。",
                    "複製エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_profileManager.IsProfileNameExists(trimmed))
            {
                MessageBox.Show(this,
                    "既に同じ名前のプロファイルが存在します。別の名前を入力してください。",
                    "プロファイル名の重複エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                var duplicated = _profileManager.DuplicateProfile(active, trimmed);
                _profileSnapshot = duplicated.Clone();
                RefreshProfileComboBox(duplicated);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "複製エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRenameProfile_Click(object sender, RoutedEventArgs e)
        {
            var active = _profileManager.ActiveProfile;
            if (active == null) return;

            string? name = InputDialog.Show(this, "プロファイル名の変更", "プロファイルの新しい名前を入力してください:", active.Name);
            Keyboard.ClearFocus();
            if (string.IsNullOrWhiteSpace(name)) return;

            string trimmed = name.Trim();
            if (string.Equals(active.Name, trimmed, StringComparison.Ordinal)) return;

            if (_profileManager.IsProfileNameExists(trimmed, active.Id))
            {
                MessageBox.Show(this,
                    "既に同じ名前のプロファイルが存在します。別の名前を入力してください。",
                    "プロファイル名の重複エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_profileManager.RenameProfile(active.Id, trimmed))
            {
                RefreshProfileComboBox(active);
            }
        }

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            var active = _profileManager.ActiveProfile;
            if (active == null) return;

            var result = MessageBox.Show(this,
                $"プロファイル「{active.Name}」を削除してもよろしいですか？",
                "プロファイルの削除確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            Keyboard.ClearFocus();

            if (result == MessageBoxResult.Yes)
            {
                _profileManager.DeleteProfile(active.Id);
                _profileSnapshot = _profileManager.ActiveProfile?.Clone();
                RefreshProfileComboBox(_profileManager.ActiveProfile);
            }
        }

        private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            if (_profileManager.ActiveProfile == null) return;

            _profileManager.SaveProfiles();
            _profileSnapshot = _profileManager.ActiveProfile.Clone();

            TxtSaveStatus.Text = "✔ 設定を保存しました";
            TxtSaveStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
            ShowSaveStatus();
        }

        private void BtnRevertProfile_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            if (_profileSnapshot == null || _profileManager.ActiveProfile == null) return;

            // スナップショットからメモリ上のActiveProfileへ復元
            var active = _profileManager.ActiveProfile;
            active.IsStickMode = _profileSnapshot.IsStickMode;
            active.Bindings.Clear();
            foreach (var kvp in _profileSnapshot.Bindings)
            {
                active.Bindings[kvp.Key] = new List<int>(kvp.Value);
            }

            ApplyProfileToUI(active);

            TxtSaveStatus.Text = "↩ 変更を破棄しました";
            TxtSaveStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
            ShowSaveStatus();
        }

        private void ShowSaveStatus()
        {
            TxtSaveStatus.Visibility = Visibility.Visible;
            _saveStatusTimer?.Stop();
            _saveStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _saveStatusTimer.Tick += (s, args) =>
            {
                TxtSaveStatus.Visibility = Visibility.Collapsed;
                _saveStatusTimer?.Stop();
            };
            _saveStatusTimer.Start();
        }

        private void ModeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_profileManager.ActiveProfile == null) return;
            _profileManager.ActiveProfile.IsStickMode = true;
            _receiver.ModeOn = true;
            ModeDescriptionText.Text = "ON: 左スティック出力中 (東方非想天則推奨 - バニラ移動対応)";
            Keyboard.ClearFocus();
        }

        private void ModeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_profileManager.ActiveProfile == null) return;
            _profileManager.ActiveProfile.IsStickMode = false;
            _receiver.ModeOn = false;
            ModeDescriptionText.Text = "OFF: D-Pad出力中 (SF6 / MBTL推奨 - POVハット対応)";
            Keyboard.ClearFocus();
        }

        private void BtnModeToggle_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();

            PicoDeviceInfo dev;
            lock (_stateLock)
            {
                dev = _latestDeviceInfo;
            }

            if (dev.Mode == PicoDeviceMode.ModeB)
            {
                // Revert to Normal Mode (Mode A)
                lock (_stateLock)
                {
                    _latestDeviceInfo = _latestDeviceInfo with { Mode = PicoDeviceMode.ModeA };
                }
                _serial.SendCommand(0x02);
            }
            else if (dev.Mode == PicoDeviceMode.ModeA)
            {
                // Start Game Mode (Mode B)
                _serial.SendCommand(0x01);
            }
        }

        private void Receiver_OnPacketGenerated(ReadOnlySpan<byte> packet, in InputState state)
        {
            lock (_stateLock)
            {
                packet.CopyTo(_latestPacket);
                _latestInputState = state;
                _packetCounter++;
                _hasNewPacket = true;
            }

            bool shouldSend;
            lock (_stateLock)
            {
                shouldSend = (_latestDeviceInfo.Mode == PicoDeviceMode.ModeB) && _serial.IsConnected;
            }

            if (shouldSend)
            {
                _serial.SendPacket(packet);
            }
        }

        private void OnDeviceStatusChanged(PicoDeviceInfo info)
        {
            lock (_stateLock)
            {
                _latestDeviceInfo = info;
            }

            if (info.Mode != PicoDeviceMode.Disconnected && !string.IsNullOrEmpty(info.PortName))
            {
                _serial.Connect(info.PortName);
            }
            else
            {
                _serial.Disconnect();
            }
        }

        private void OnSerialConnected(string portName)
        {
            Dispatcher.InvokeAsync(UpdateConnectionBadge);
        }

        private void OnSerialDisconnected()
        {
            Dispatcher.InvokeAsync(UpdateConnectionBadge);
        }

        private void UpdateConnectionBadge()
        {
            PicoDeviceInfo dev;
            bool connected = _serial.IsConnected;
            string? port = _serial.CurrentPort;

            lock (_stateLock)
            {
                dev = _latestDeviceInfo;
            }

            if (!connected || dev.Mode == PicoDeviceMode.Disconnected)
            {
                HwStatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A));
                HwStatusBadge.Text = "接続状態: 未接続";
                HwStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(0xF9, 0xE2, 0xAF));

                BtnModeToggle.IsEnabled = false;
                BtnModeToggle.Content = "Pico未接続 (待機中)";
                BtnModeToggle.Background = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A));
                BtnModeToggle.Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8));
            }
            else if (dev.Mode == PicoDeviceMode.ModeB)
            {
                HwStatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
                HwStatusBadge.Text = $"ゲームモード接続中: {port} (XInput)";
                HwStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x1B));

                BtnModeToggle.IsEnabled = true;
                BtnModeToggle.Content = "ゲームモード稼働中 → クリックで停止";
                BtnModeToggle.Background = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
                BtnModeToggle.Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x1B));
            }
            else // Mode A
            {
                HwStatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA));
                HwStatusBadge.Text = $"通常モード待機中: {port}";
                HwStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x1B));

                BtnModeToggle.IsEnabled = true;
                BtnModeToggle.Content = "通常モード (待機中) → クリックでゲームモード起動";
                BtnModeToggle.Background = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA));
                BtnModeToggle.Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x1B));
            }
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            byte[] packetCopy = new byte[6];
            InputState stateCopy;
            ulong count;
            bool updated;

            lock (_stateLock)
            {
                updated = _hasNewPacket;
                _hasNewPacket = false;
                Array.Copy(_latestPacket, packetCopy, 6);
                stateCopy = _latestInputState;
                count = _packetCounter;
            }

            UpdateConnectionBadge();

            if (!updated && count == 0) return;

            // Update Hex display
            HexByte0.Text = packetCopy[0].ToString("X2");
            HexByte1.Text = packetCopy[1].ToString("X2");
            HexByte2.Text = packetCopy[2].ToString("X2");
            HexByte3.Text = packetCopy[3].ToString("X2");
            HexByte4.Text = packetCopy[4].ToString("X2");
            HexByte5.Text = packetCopy[5].ToString("X2");

            PacketRateText.Text = $"パケット数: {count:N0}";

            // Update Indicators independently for all buttons
            UpdateVirtualPadVisuals(in stateCopy);
        }

        private void UpdateVirtualPadVisuals(in InputState state)
        {
            UpdateIndicator(Ind_Up, Img_Up_pressed, state.Up);
            UpdateIndicator(Ind_Down, Img_Down_pressed, state.Down);
            UpdateIndicator(Ind_Left, Img_Left_pressed, state.Left);
            UpdateIndicator(Ind_Right, Img_Right_pressed, state.Right);

            UpdateIndicator(Ind_A, Img_A_pressed, state.A);
            UpdateIndicator(Ind_B, Img_B_pressed, state.B);
            UpdateIndicator(Ind_X, Img_X_pressed, state.X);
            UpdateIndicator(Ind_Y, Img_Y_pressed, state.Y);

            UpdateIndicator(Ind_LB, Img_LB_pressed, state.LB);
            UpdateIndicator(Ind_RB, Img_RB_pressed, state.RB);
            UpdateIndicator(Ind_L3, Img_L3_pressed, state.L3);
            UpdateIndicator(Ind_R3, Img_R3_pressed, state.R3);

            UpdateIndicator(Ind_LT, Img_LT_pressed, state.LT);
            UpdateIndicator(Ind_RT, Img_RT_pressed, state.RT);

            UpdateIndicator(Ind_Start, Img_Start_pressed, state.Start);
            UpdateIndicator(Ind_Back, Img_Back_pressed, state.Back);
            UpdateIndicator(Ind_Guide, Img_Guide_pressed, state.Guide);
        }

        private void UpdateIndicator(Border border, Image imgPressed, bool active)
        {
            string? buttonName = border.Tag as string;
            bool isWaiting = buttonName != null && buttonName == _waitingBindingButton;

            if (isWaiting)
            {
                border.BorderBrush = _waitingBorderBrush;
                border.BorderThickness = new Thickness(2);
                border.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xF9, 0xE2, 0xAF));
                imgPressed.Visibility = Visibility.Hidden;
            }
            else
            {
                border.BorderBrush = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
                border.Background = Brushes.Transparent;
                imgPressed.Visibility = active ? Visibility.Visible : Visibility.Hidden;
            }
        }

        #region Key Binding Configuration

        private void BindingTarget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // プロファイル0件の場合は設定不可
            if (_profileManager.ActiveProfile == null)
            {
                e.Handled = true;
                return;
            }

            if (sender is Border border && border.Tag is string buttonName)
            {
                _waitingBindingButton = buttonName;
                RefreshAllBindingLabels();
                e.Handled = true;

                // Windowに確実にキーボードフォーカスを当ててPreviewKeyDownを有効化
                this.Focus();
                Keyboard.Focus(this);
            }
        }

        private void BindingTarget_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // プロファイル0件の場合は何もしない
            if (_profileManager.ActiveProfile == null)
            {
                e.Handled = true;
                return;
            }

            if (sender is Border border && border.Tag is string buttonName)
            {
                e.Handled = true;
                ClearButtonBindings(buttonName);
                if (_waitingBindingButton == buttonName)
                {
                    _waitingBindingButton = null;
                }
                Keyboard.ClearFocus();
                RefreshAllBindingLabels();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (string.IsNullOrEmpty(_waitingBindingButton) || _profileManager.ActiveProfile == null) return;

            // Space, Enter, Tab等を含め全キーの標準フォーカス移動・ボタン発火を完全に握りつぶす
            e.Handled = true;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.ImeProcessed)
            {
                key = e.ImeProcessedKey;
            }

            if (key == Key.Escape)
            {
                // Esc: 当該ボタンのバインドを空リスト化（全クリア）
                ClearButtonBindings(_waitingBindingButton);
            }
            else
            {
                int vk = KeyInterop.VirtualKeyFromKey(key);
                if (vk > 0)
                {
                    AddButtonBinding(_waitingBindingButton, vk);
                }
            }

            _waitingBindingButton = null;
            Keyboard.ClearFocus();
            RefreshAllBindingLabels();
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            // アプリ外へのフォーカス移動時に安全に入力待ちを解除
            if (!string.IsNullOrEmpty(_waitingBindingButton))
            {
                _waitingBindingButton = null;
                RefreshAllBindingLabels();
            }
        }

        private void AddButtonBinding(string buttonName, int vkCode)
        {
            var profile = _profileManager.ActiveProfile;
            if (profile == null) return;

            if (!profile.Bindings.TryGetValue(buttonName, out var list))
            {
                list = new List<int>();
                profile.Bindings[buttonName] = list;
            }

            if (!list.Contains(vkCode))
            {
                list.Add(vkCode);
            }

            // メモリ上と受信機へ即座に反映（ファイル保存は「保存」ボタン押下時）
            _receiver.SetProfile(profile);
        }

        private void ClearButtonBindings(string buttonName)
        {
            var profile = _profileManager.ActiveProfile;
            if (profile == null) return;

            if (profile.Bindings.TryGetValue(buttonName, out var list))
            {
                list.Clear();
            }
            else
            {
                profile.Bindings[buttonName] = new List<int>();
            }

            // メモリ上と受信機へ即座に反映（ファイル保存は「保存」ボタン押下時）
            _receiver.SetProfile(profile);
        }

        private void RefreshAllBindingLabels()
        {
            var profile = _profileManager.ActiveProfile;

            UpdateSingleLabel(Ind_LB, Txt_LB, "LB", profile);
            UpdateSingleLabel(Ind_RB, Txt_RB, "RB", profile);
            UpdateSingleLabel(Ind_LT, Txt_LT, "LT", profile);
            UpdateSingleLabel(Ind_RT, Txt_RT, "RT", profile);

            UpdateSingleLabel(Ind_Back, Txt_Back, "Back", profile);
            UpdateSingleLabel(Ind_Guide, Txt_Guide, "Guide", profile);
            UpdateSingleLabel(Ind_Start, Txt_Start, "Start", profile);

            UpdateSingleLabel(Ind_Up, Txt_Up, "Up", profile);
            UpdateSingleLabel(Ind_Down, Txt_Down, "Down", profile);
            UpdateSingleLabel(Ind_Left, Txt_Left, "Left", profile);
            UpdateSingleLabel(Ind_Right, Txt_Right, "Right", profile);

            UpdateSingleLabel(Ind_Y, Txt_Y, "Y", profile);
            UpdateSingleLabel(Ind_X, Txt_X, "X", profile);
            UpdateSingleLabel(Ind_B, Txt_B, "B", profile);
            UpdateSingleLabel(Ind_A, Txt_A, "A", profile);

            UpdateSingleLabel(Ind_L3, Txt_L3, "L3", profile);
            UpdateSingleLabel(Ind_R3, Txt_R3, "R3", profile);
        }

        private void UpdateSingleLabel(Border border, OutlinedTextBlock textBlock, string buttonKey, GamepadProfile? profile)
        {
            bool isWaiting = profile != null && buttonKey == _waitingBindingButton;
            UpdateSingleLabel(textBlock, buttonKey, profile, isWaiting);

            if (isWaiting)
            {
                border.BorderBrush = _waitingBorderBrush;
                border.BorderThickness = new Thickness(2);
                border.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xF9, 0xE2, 0xAF));
            }
            else
            {
                border.BorderBrush = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
                border.Background = Brushes.Transparent;
            }
        }

        private void UpdateSingleLabel(OutlinedTextBlock textBlock, string buttonKey, GamepadProfile? profile, bool isWaiting)
        {
            if (isWaiting)
            {
                textBlock.Text = "[待機中]";
                textBlock.Foreground = _waitingBorderBrush;
            }
            else
            {
                if (profile != null && profile.Bindings.TryGetValue(buttonKey, out var list) && list.Count > 0)
                {
                    string keysText = string.Join(", ", list.Select(vk => GetKeyName(vk)));
                    textBlock.Text = keysText;
                }
                else
                {
                    textBlock.Text = "(未設定)";
                }
                textBlock.Foreground = _inactiveFgBrush;
            }
        }

        public static string GetKeyName(int vkCode)
        {
            if (vkCode <= 0) return "未設定";

            Key key = KeyInterop.KeyFromVirtualKey(vkCode);
            return key switch
            {
                Key.None => "未設定",
                Key.Return => "Enter",
                Key.Back => "BS",
                Key.Escape => "Esc",
                Key.Space => "Space",
                Key.Left => "←",
                Key.Right => "→",
                Key.Up => "↑",
                Key.Down => "↓",
                Key.LeftShift => "L-Shift",
                Key.RightShift => "R-Shift",
                Key.LeftCtrl => "L-Ctrl",
                Key.RightCtrl => "R-Ctrl",
                Key.LeftAlt => "L-Alt",
                Key.RightAlt => "R-Alt",
                Key.Tab => "Tab",
                Key.CapsLock => "Caps",
                Key.Delete => "Del",
                Key.Insert => "Ins",
                Key.Home => "Home",
                Key.End => "End",
                Key.PageUp => "PgUp",
                Key.PageDown => "PgDn",
                _ => key.ToString()
            };
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _uiTimer.Stop();
            _deviceWatcher.Dispose();
            _serial.Dispose();
            _receiver.Dispose();
            base.OnClosed(e);
        }
    }
}
