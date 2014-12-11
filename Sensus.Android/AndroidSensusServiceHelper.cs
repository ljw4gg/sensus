using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Widget;
using Java.Lang;
using SensusService;
using System.IO;
using Xamarin.Geolocation;

namespace Sensus.Android
{
    public class AndroidSensusServiceHelper : SensusServiceHelper
    {
        private static string _preventAutoRestartPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "no_autorestart");

        private ConnectivityManager _connectivityManager;
        private readonly string _deviceId;

        public override bool WiFiConnected
        {
            get { return _connectivityManager.GetNetworkInfo(ConnectivityType.Wifi).IsConnected; }
        }

        public override bool IsCharging
        {
            get
            {
                IntentFilter filter = new IntentFilter(Intent.ActionBatteryChanged);
                BatteryStatus status = (BatteryStatus)Application.Context.RegisterReceiver(null, filter).GetIntExtra(BatteryManager.ExtraStatus, -1);
                return status == BatteryStatus.Charging || status == BatteryStatus.Full;
            }
        }

        public override string DeviceId
        {
            get { return _deviceId; }
        }

        public AndroidSensusServiceHelper()
            : base(new Geolocator(Application.Context), !File.Exists(_preventAutoRestartPath))
        {
            _connectivityManager = Application.Context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
            _deviceId = Settings.Secure.GetString(Application.Context.ContentResolver, Settings.Secure.AndroidId);
        }       

        public override void ShareFile(string path, string emailSubject)
        {
            try
            {
                Intent intent = new Intent(Intent.ActionSend);
                intent.SetType("text/plain");
                intent.AddFlags(ActivityFlags.NewTask);

                if (!string.IsNullOrWhiteSpace(emailSubject))
                    intent.PutExtra(Intent.ExtraSubject, emailSubject);

                Java.IO.File file = new Java.IO.File(path);
                file.SetReadable(true, false);
                global::Android.Net.Uri uri = global::Android.Net.Uri.FromFile(file);
                intent.PutExtra(Intent.ExtraStream, uri);

                Application.Context.StartActivity(intent);
            }
            catch (Exception ex) { Logger.Log("Failed to start intent to share file \"" + path + "\":  " + ex.Message, LoggingLevel.Normal); }
        }

        protected override void SetAutoRestart(bool enabled)
        {
            Context context = Application.Context;
            AlarmManager alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager;
            Intent serviceIntent = new Intent(context, typeof(AndroidSensusService));
            PendingIntent pendingServiceIntent = PendingIntent.GetService(context, 0, serviceIntent, PendingIntentFlags.UpdateCurrent);

            if (enabled)
            {
                if (File.Exists(_preventAutoRestartPath))
                    File.Delete(_preventAutoRestartPath);

                long nextAlarmMS = JavaSystem.CurrentTimeMillis() + 5000;
                alarmManager.SetRepeating(AlarmType.RtcWakeup, nextAlarmMS, 1000 * 60, pendingServiceIntent);
                Toast.MakeText(context, "Sensus auto-restart has been enabled.", ToastLength.Long).Show();
            }
            else
            {
                if (!File.Exists(_preventAutoRestartPath))
                    File.Create(_preventAutoRestartPath);

                alarmManager.Cancel(pendingServiceIntent);
                Toast.MakeText(context, "Sensus auto-restart has been disabled.", ToastLength.Long).Show();
            }
        }
    }
}