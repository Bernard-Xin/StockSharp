namespace SampleAsyncTransactions
{
	using System;
	using System.Collections.Generic;
	using System.Windows;

	using Ecng.Collections;
	using Ecng.Xaml;

	using StockSharp.BusinessEntities;
	using StockSharp.Messages;
	using StockSharp.Localization;

	public partial class SecuritiesWindow
	{
		private readonly SynchronizedDictionary<Security, QuotesWindow> _quotesWindows = new SynchronizedDictionary<Security, QuotesWindow>();
		private bool _initialized;

		public SecuritiesWindow()
		{
			InitializeComponent();
		}

		protected override void OnClosed(EventArgs e)
		{
			var trader = MainWindow.Instance.Trader;
			if (trader != null)
			{
				if (_initialized)
					trader.MarketDepthsChanged -= TraderOnMarketDepthsChanged;

				_quotesWindows.SyncDo(d =>
				{
					foreach (var pair in d)
					{
						MainWindow.Instance.Trader.UnRegisterMarketDepth(pair.Key);

						pair.Value.DeleteHideable();
						pair.Value.Close();
					}
				});	
			}

			base.OnClosed(e);
		}

		private void SecurityPicker_OnSecuritySelected(Security security)
		{
			RegisterErrorOrder.IsEnabled = Quotes.IsEnabled = security != null;
		}

		private void QuotesClick(object sender, RoutedEventArgs e)
		{
			var trader = MainWindow.Instance.Trader;

			var window = _quotesWindows.SafeAdd(SecurityPicker.SelectedSecurity, security =>
			{
				// начинаем получать котировку стакана
				trader.RegisterMarketDepth(security);

				// создаем окно со стаканом
				var wnd = new QuotesWindow { Title = security.Id + LocalizedStrings.Str2957 };
				wnd.MakeHideable();
				return wnd;
			});

			if (window.Visibility == Visibility.Visible)
				window.Hide();
			else
				window.Show();

			if (!_initialized)
			{
				TraderOnMarketDepthsChanged(new[] { trader.GetMarketDepth(SecurityPicker.SelectedSecurity) });
				trader.MarketDepthsChanged += TraderOnMarketDepthsChanged;
				_initialized = true;
			}
		}

		private void RegisterErrorOrderClick(object sender, RoutedEventArgs e)
		{
			var security = SecurityPicker.SelectedSecurity;

			MainWindow.Instance.Trader.RegisterOrder(new Order
			{
				Security = SecurityPicker.SelectedSecurity,
				Direction = Sides.Buy,
				Price = security.BestBid != null ? security.BestBid.Price : 1,
				Volume = -1,
				Portfolio = MainWindow.Instance.Portfolio,
			});
		}

		private void TraderOnMarketDepthsChanged(IEnumerable<MarketDepth> depths)
		{
			foreach (var depth in depths)
			{
				var wnd = _quotesWindows.TryGetValue(depth.Security);

				if (wnd != null)
					wnd.DepthCtrl.UpdateDepth(depth);
			}
		}
	}
}