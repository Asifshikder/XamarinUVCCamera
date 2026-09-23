using System;
using System.ComponentModel;
using Android.Content;
using Android.Widget;
using CameraUVC;
using CameraUVC.Droid.Renderers;
using CameraUVC.Models;
using Com.Serenegiant.Usb.Widget;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using ARelativeLayout = Android.Widget.RelativeLayout;

[assembly: ExportRenderer(typeof(UvcCameraView), typeof(UvcCameraRenderer))]

namespace CameraUVC.Droid.Renderers
{
    /// <summary>
    /// Custom Xamarin.Forms ViewRenderer mapping cross-platform UvcCameraView
    /// to the native Android UVCCameraTextureView surface for hardware-accelerated preview.
    /// </summary>
    public class UvcCameraRenderer : ViewRenderer<UvcCameraView, ARelativeLayout>
    {
        internal UVCCameraTextureView UvcCamera;

        public UvcCameraRenderer(Context context) : base(context)
        {
        }

        protected override void OnElementChanged(ElementChangedEventArgs<UvcCameraView> args)
        {
            base.OnElementChanged(args);

            if (args.NewElement != null)
            {
                if (Control == null)
                {
                    // Initialize the native hardware video texture view
                    UvcCamera = new UVCCameraTextureView(Context);

                    // Host the VideoView within an Android RelativeLayout
                    var relativeLayout = new ARelativeLayout(Context);
                    relativeLayout.AddView(UvcCamera);

                    // Center the VideoView in the RelativeLayout
                    var layoutParams = new ARelativeLayout.LayoutParams(
                        LayoutParams.MatchParent,
                        LayoutParams.MatchParent);
                    layoutParams.AddRule(LayoutRules.CenterInParent);
                    UvcCamera.LayoutParameters = layoutParams;

                    SetNativeControl(relativeLayout);
                }

                // Subscribe to camera action requests from the cross-platform element
                args.NewElement.OpenRequested += NewElement_OpenRequested;
                args.NewElement.CloseRequested += NewElement_CloseRequested;
                args.NewElement.StartRecordingRequested += NewElement_StartRecordingRequested;
                args.NewElement.StopRecordingRequested += NewElement_StopRecordingRequested;
                args.NewElement.TakeSnapshotRequested += NewElement_TakeSnapshotRequested;
            }

            if (args.OldElement != null)
            {
                // Unsubscribe to prevent memory leaks
                args.OldElement.OpenRequested -= NewElement_OpenRequested;
                args.OldElement.CloseRequested -= NewElement_CloseRequested;
                args.OldElement.StartRecordingRequested -= NewElement_StartRecordingRequested;
                args.OldElement.StopRecordingRequested -= NewElement_StopRecordingRequested;
                args.OldElement.TakeSnapshotRequested -= NewElement_TakeSnapshotRequested;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Control != null && UvcCamera != null)
                {
                    UvcCamera = null;
                }
            }

            base.Dispose(disposing);
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            base.OnElementPropertyChanged(sender, args);

            // Handle live property changes such as rotation or flipping if updated dynamically
        }

        private void NewElement_OpenRequested(object sender, RequestOpenArgs e)
        {
            var cameraView = (UvcCameraView)sender;
            UvcCameraHelper.Setup(cameraView, new UvcCameraSetupOptions
            {
                PreviewHeight = e.PreviewHeight,
                PreviewWidth = e.PreviewWidth,
                VideoRotation = cameraView.VideoRotation,
                FlipVertically = cameraView.FlipVertically,
                FlipHorizontally = cameraView.FlipHorizontally
            });
            UvcCameraHelper.StartPreview(cameraView);
        }

        private void NewElement_CloseRequested(object sender, EventArgs e)
        {
            UvcCameraHelper.StopPreview();
        }

        private void NewElement_StartRecordingRequested(object sender, RequestStartRecordingArgs e)
        {
            UvcCameraHelper.StartRecording(e.VideoPath);
        }

        private void NewElement_StopRecordingRequested(object sender, EventArgs e)
        {
            UvcCameraHelper.StopRecording();
        }

        private void NewElement_TakeSnapshotRequested(object sender, RequestTakeSnapshotArgs e)
        {
            UvcCameraHelper.CapturePicture(e.ImagePath, (file) =>
            {
                Console.WriteLine($"[UvcCameraRenderer] Snapshot saved to: {file}");
            });
        }
    }
}
