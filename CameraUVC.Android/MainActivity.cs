using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using System.IO;
using CameraUVC.Interfaces;

namespace CameraUVC.Droid
{
    [Activity(Label = "CameraUVC",
        Icon = "@mipmap/icon",
        Theme = "@style/MainTheme",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        public static MainActivity Instance { get; private set; }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            Instance = this;
            base.OnCreate(savedInstanceState);

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);

            try
            {
                InitializeCameraHelper();
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[CameraUVC] Error initializing CameraHelper: {ex}");
            }

            LoadApplication(new App());
        }

        protected override void OnResume()
        {
            base.OnResume();
            Instance = this;
            if (CameraHelper.Instance == null)
            {
                try
                {
                    InitializeCameraHelper();
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"[CameraUVC] Error initializing CameraHelper (OnResume): {ex}");
                }
            }
        }

        private void InitializeCameraHelper()
        {
            // Use app-specific external storage for compatibility with all Android versions
            var appRoot = GetExternalFilesDir(null).AbsolutePath;
            var photosDir = Path.Combine(appRoot, "simple_uvc_camera", "photos");
            var videosDir = Path.Combine(appRoot, "simple_uvc_camera", "videos");
            Directory.CreateDirectory(photosDir);
            Directory.CreateDirectory(videosDir);

            CameraHelper.Instance = new CameraHelperImpl
            {
                PhotoRootDir = photosDir,
                VideoRootDir = videosDir,
            };

            System.Console.WriteLine($"Photos will be saved to: {photosDir}");
            System.Console.WriteLine($"Videos will be saved to: {videosDir}");
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}