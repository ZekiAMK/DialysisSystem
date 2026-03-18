using MAUIAapp.Control;

namespace MAUIAapp;

public partial class MainPage : ContentPage
{
	int count = 0;
	private CancellationTokenSource? _cts;

	public MainPage()
	{
		InitializeComponent();
	}
	private void OnCounterClicked(object? sender, EventArgs e)
	{
		count++;

		if (count == 1)
			CounterBtn.Text = $"Clicked {count} time";
		else
			CounterBtn.Text = $"Clicked {count} times";

		SemanticScreenReader.Announce(CounterBtn.Text);
	}
	private async void OnStartSDLClicked(object? sender, EventArgs e)
	{
	#if ANDROID
		var context = Android.App.Application.Context;
		var intent = new Android.Content.Intent(context,
			Java.Lang.Class.ForName("com.companyname.mauiaapp.DoomActivity"));
		intent.PutExtra("wadPath", "/data/data/com.companyname.mauiaapp/files/doom1.wad");
		intent.AddFlags(Android.Content.ActivityFlags.NewTask);
		context.StartActivity(intent);
	#endif
	}
}
