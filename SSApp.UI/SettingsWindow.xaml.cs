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
    }
}
