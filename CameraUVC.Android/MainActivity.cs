using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;

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

            // Create directories for photos and videos
            var photosDir = GetExternalFilesDir(Environment.DirectoryPictures).AbsolutePath;
            var videosDir = GetExternalFilesDir(Environment.DirectoryMovies).AbsolutePath;
            
            // Ensure directories exist
            System.IO.Directory.CreateDirectory(photosDir);
            System.IO.Directory.CreateDirectory(videosDir);
            
            CameraHelper.Instance = new CameraHelperImpl
            {
                PhotoRootDir = photosDir,
                VideoRootDir = videosDir,
            };
            
            System.Console.WriteLine($"Photos will be saved to: {photosDir}");
            System.Console.WriteLine($"Videos will be saved to: {videosDir}");
            
            LoadApplication(new App());
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}