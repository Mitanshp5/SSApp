using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using SSApp.Services.Notifications; // Assuming this exists based on previous context
using SSApp.Services.Logging; // Assuming this exists

namespace SSApp.UI
{
    public partial class SettingsWindow : Window
    {
        // Native Imports
        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int SetCameraExposureAuto(int mode);

        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int SetCameraExposureTime(float exposureTimeUs);

        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetCameraExposureAuto();

        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern float GetCameraExposureTime();

        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void SetPlcBit(string device, int value);

        private bool _isInitialized = false;

        public SettingsWindow()
        {
            InitializeComponent();
            this.Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCameraSettings();
            _isInitialized = true;
        }

        private void LoadCameraSettings()
        {
            try
            {
                SetCameraExposureAuto(2);
                // Get Auto Mode
                // 0=Off, 1=Once, 2=Continuous
                int autoMode = GetCameraExposureAuto();
                if (autoMode >= 0)
                {
                    // Select item by Tag
                    foreach (ComboBoxItem item in ExposureModeCombo.Items)
                    {
                        if (item.Tag?.ToString() == autoMode.ToString())
                        {
                            ExposureModeCombo.SelectedItem = item;
                            break;
                        }
                    }
                    UpdateSliderState(autoMode);
                }

                // Get Exposure Time
                float expTime = GetCameraExposureTime();
                if (expTime > 0)
                {
                    ExposureSlider.Value = expTime;
                    ExposureTextBox.Text = expTime.ToString("F0");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load camera settings", ex);
                // NotificationService.ShowError("Failed to read settings from camera.");
            }
        }

        private void UpdateSliderState(int autoMode)
        {
            // If Auto (1 or 2), disable manual time slider
            if (autoMode == 0) // Off / Manual
            {
                ExposureTimeContainer.Opacity = 1.0;
                ExposureSlider.IsEnabled = true;
                ExposureTextBox.IsEnabled = true;
            }
            else
            {
                ExposureTimeContainer.Opacity = 0.5;
                ExposureSlider.IsEnabled = false;
                ExposureTextBox.IsEnabled = false;
            }
        }

        private void ExposureModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (ExposureModeCombo.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                if (int.TryParse(item.Tag.ToString(), out int mode))
                {
                    int ret = SetCameraExposureAuto(mode);
                    if (ret != 0) // MV_OK = 0
                    {
                         NotificationService.ShowError($"Failed to set Exposure Mode. Error: {ret}");
                         // Revert?
                    }
                    else
                    {
                        UpdateSliderState(mode);
                    }
                }
            }
        }

        private void ExposureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;
            
            // Only update if in Manual mode
            if (ExposureSlider.IsEnabled)
            {
                SetCameraExposureTime((float)e.NewValue);
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadCameraSettings();
            NotificationService.ShowInfo("Camera settings refreshed.");
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private bool _suppressLightEvents = false;

        private void CalibrationMode_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressLightEvents) return;

            _suppressLightEvents = true;
            ChkY1.IsChecked = true;
            ChkY3.IsChecked = true;
            ChkY4.IsChecked = true;
            ChkY5.IsChecked = true;
            _suppressLightEvents = false;

            SetLights(1);
        }

        private void CalibrationMode_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressLightEvents) return;

            _suppressLightEvents = true;
            ChkY1.IsChecked = false;
            ChkY3.IsChecked = false;
            ChkY4.IsChecked = false;
            ChkY5.IsChecked = false;
            _suppressLightEvents = false;

            SetLights(0);
        }

        private void Light_Checked(object sender, RoutedEventArgs e)
        {
            HandleLightToggle(sender, 1);
        }

        private void Light_Unchecked(object sender, RoutedEventArgs e)
        {
            HandleLightToggle(sender, 0);
        }

        private void HandleLightToggle(object sender, int value)
        {
            if (_suppressLightEvents) return;

            if (sender is CheckBox cb && cb.Tag is string device)
            {
                try
                {
                    SetPlcBit(device, value);
                    
                    // Sync "All" checkbox state
                    _suppressLightEvents = true;
                    if (IsAllChecked())
                    {
                        ChkAllLights.IsChecked = true;
                    }
                    else if (value == 0) // If any unchecked, uncheck All
                    {
                        ChkAllLights.IsChecked = false;
                    }
                    _suppressLightEvents = false;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to set {device}", ex);
                }
            }
        }

        private bool IsAllChecked()
        {
            return (ChkY1.IsChecked == true) && 
                   (ChkY3.IsChecked == true) && 
                   (ChkY4.IsChecked == true) && 
                   (ChkY5.IsChecked == true);
        }

        private void SetLights(int value)
        {
            try
            {
                SetPlcBit("Y1", value);
                SetPlcBit("Y3", value);
                SetPlcBit("Y4", value);
                SetPlcBit("Y5", value);
                
                if (value == 1)
                    NotificationService.ShowInfo("Calibration lights ON");
                else
                    NotificationService.ShowInfo("Calibration lights OFF");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to toggle lights", ex);
            }
        }
    }
}
