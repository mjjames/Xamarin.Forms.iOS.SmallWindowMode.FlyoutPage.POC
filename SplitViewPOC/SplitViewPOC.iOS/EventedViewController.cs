using System;
using UIKit;

namespace Xamarin.Forms.Platform.iOS
{
	internal class EventedViewController : ChildViewController
	{
		FlyoutView _flyoutView;

		event EventHandler _didAppear;
		event EventHandler _willDisappear;

		public EventedViewController()
		{
			_flyoutView = new FlyoutView();
		}


		public event EventHandler DidAppear
		{
			add
			{
				_flyoutView.DidAppear += value;
				_didAppear += value;
			}
			remove
			{
				_flyoutView.DidAppear -= value;
				_didAppear -= value;
			}
		}

		public event EventHandler WillDisappear
		{
			add
			{
				_flyoutView.WillDisappear += value;
				_willDisappear += value;
			}
			remove
			{
				_flyoutView.WillDisappear -= value;
				_willDisappear -= value;
			}
		}

		public override void ViewDidAppear(bool animated)
		{
			base.ViewDidAppear(animated);
			_didAppear?.Invoke(this, EventArgs.Empty);
		}

		public override void ViewWillDisappear(bool animated)
		{
			base.ViewWillDisappear(animated);
			_willDisappear?.Invoke(this, EventArgs.Empty);
		}

		public override void ViewDidDisappear(bool animated)
		{
			base.ViewDidDisappear(animated);
			_willDisappear?.Invoke(this, EventArgs.Empty);
		}

		public override void LoadView()
		{
			View = _flyoutView;
		}

		public class FlyoutView : UIView
		{
			public bool IsCollapsed => Center.X <= 0;
			bool _previousIsCollapsed = true;

			public event EventHandler DidAppear;
			public event EventHandler WillDisappear;

			// this only gets called on iOS12 everytime it's collapsed or expanded
			// I haven't found an override on iOS13 that gets called but it doesn't seem
			// to matter because the DidAppear and WillDisappear seem more consistent on iOS 13
			public override void LayoutSubviews()
			{
				base.LayoutSubviews();
				UpdateCollapsedSetting();
			}

			void UpdateCollapsedSetting()
			{
				if (_previousIsCollapsed != IsCollapsed)
				{
					_previousIsCollapsed = IsCollapsed;

					if (IsCollapsed)
						WillDisappear?.Invoke(this, EventArgs.Empty);
					else
						DidAppear?.Invoke(this, EventArgs.Empty);
				}
			}
		}
	}
}