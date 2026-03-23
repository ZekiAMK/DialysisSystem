using mobile.Models;
using mobile.PageModels;
using mobile.Control;

namespace mobile.Pages
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }

        private async void OnStartSDLClicked(object? sender, EventArgs e)
        {
        #if ANDROID
            var wadPath = await WadHelper.ExtractWadAsync();

            var context = Android.App.Application.Context;
            var intent = new Android.Content.Intent(context,
                Java.Lang.Class.ForName("com.companyname.mobile.DoomActivity"));
            intent.PutExtra("wadPath",wadPath);
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);
            context.StartActivity(intent);
        #endif
        }
        
    }
}