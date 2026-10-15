using System;
using Windows.ApplicationModel.Activation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KNASXbox
{
    sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            // No mouse cursor on Xbox: the controller works like a TV remote.
            RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;
            // Let channels start with sound by themselves, like a TV.
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--autoplay-policy=no-user-gesture-required");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            // Use the whole screen (Xbox otherwise keeps a border for old TVs).
            try { ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow); } catch { }
            var frame = Window.Current.Content as Frame;
            if (frame == null)
            {
                frame = new Frame();
                Window.Current.Content = frame;
            }
            if (frame.Content == null) frame.Navigate(typeof(MainPage), e.Arguments);
            Window.Current.Activate();
        }
    }
}
