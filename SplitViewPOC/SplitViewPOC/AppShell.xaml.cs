using SplitViewPOC.ViewModels;
using SplitViewPOC.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace SplitViewPOC
{
	public partial class AppShell : Xamarin.Forms.FlyoutPage
	{
		private bool _isInSmallWindowMode;

		public AppShell()
		{
			InitializeComponent();
			IsPresentedChanged += AppShell_IsPresentedChanged;
		}

		private void AppShell_IsPresentedChanged(object sender, EventArgs e)
		{
			Debug.WriteLine($"Is Presented Changed: {IsPresented} Modal Items? {Navigation.ModalStack.Count}");
			//todo need to pop the modal to show the navigation again.....
		}

		private async void ToolbarItem_Clicked(object sender, EventArgs e)
		{
			try
			{
				await ShowSmallWindowNavigation();
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
			}
			IsPresented = true;
		}

		private Task ShowSmallWindowNavigation()
		{
			var nav = Detail;
			nav.Parent = null;
			// we need to use the base Navigation here as we want to use 
			// the navigation that can't show modals on top of everything, so the Flyout Pages one
			if (base.Navigation.ModalStack.Contains(nav))
			{
				return base.Navigation.PopModalAsync();
			}
			return base.Navigation.PushModalAsync(nav);
		}

		private void Button_Clicked(object sender, EventArgs e)
		{
			Navigation.PushAsync(new AboutPage());
		}

		public new INavigation Navigation
		{
			get
			{
				if (_isInSmallWindowMode)
				{
					Debug.WriteLine("In Small Window Mode, using Flyout Nav");
					return Flyout.Navigation;
				}
				Debug.WriteLine("Not In Small Window Mode, using Detail Nav");
				return Detail.Navigation;
			}
		}

		protected override void OnAppearing()
		{
			base.OnAppearing();
			EnterSmallWindowMode();
		}

		private void EnterSmallWindowMode()
		{
			var teamRed = Flyout;
			var teamBlue = Detail;
			teamRed.Parent = null;
			teamBlue.Parent = null;
			Flyout = teamBlue;
			Detail = teamRed;
			_isInSmallWindowMode = true;
		}
	}
}
