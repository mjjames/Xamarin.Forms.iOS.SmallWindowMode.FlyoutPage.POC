﻿using SplitViewPOC.ViewModels;
using SplitViewPOC.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace SplitViewPOC
{
	public partial class AppShell : Xamarin.Forms.FlyoutPage
	{
		private bool _isInSmallWindowMode;

		public AppShell()
		{
			InitializeComponent();
		}

		private void AppShell_IsPresentedChanged(object sender, EventArgs e)
		{
			Debug.WriteLine($"Is Presented Changed: {IsPresented} Modal Items? {Navigation.ModalStack.Count}");
			//todo need to pop the modal to show the navigation again.....
		}

		private void ToolbarItem_Clicked(object sender, EventArgs e)
		{
			if (CanChangeIsPresented)
			{
				IsPresented = !IsPresented;
			}
		}

		private void Button_Clicked(object sender, EventArgs e)
			=> Navigation.PushAsync(new AboutPage());

		public new INavigation Navigation
		{
			get
			{
				Debug.WriteLine($"Current Width {Width} | Flyout Width: {Flyout.Width} | Detail Width: {Detail.Width} | Is Presented: {IsPresented}");

				//if (this.Width <= 375)
				//{
				//	Debug.WriteLine("In Small Window Mode - Use Detail Nav");
					return Detail.Navigation;
				//}
				//Debug.WriteLine("Using Base Nav");
				//return base.Navigation;
			}
		}


	}
}
